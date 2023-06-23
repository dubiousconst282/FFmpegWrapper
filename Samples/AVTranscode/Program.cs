using System.Diagnostics;

using FFmpeg.Wrapper;

if (args.Length < 2) {
    //Note that not all formats support the hardcoded codec settings used here.
    //In this case only .mkv was tested.
    Console.WriteLine("Usage: AVTranscode <input path> <output path.mkv>");
    return;
}
using var demuxer = new MediaDemuxer(args[0]);
using var muxer = new MediaMuxer(args[1]);

var transcoders = new List<MediaTranscoder?>(demuxer.Streams.Length);

foreach (var stream in demuxer.Streams) {
    transcoders.Add(stream.Type switch {
        MediaTypes.Audio => new AudioTranscoder(muxer, demuxer, stream),
        MediaTypes.Video => new VideoTranscoder(muxer, demuxer, stream),
        _ => null
    });
}
muxer.Open();

using var packet = new MediaPacket();
var sw = Stopwatch.StartNew();
var lastPos = TimeSpan.Zero;

while (demuxer.Read(packet)) {
    transcoders[packet.StreamIndex]?.ReceivePacket(packet);
    
    if (sw.ElapsedMilliseconds >= 1000) {
        var stream = demuxer.Streams[packet.StreamIndex];
        var currentPos = stream.GetTimestamp(packet.PresentationTimestamp!.Value);
        Console.WriteLine($"Progress: {currentPos:mm\\:ss}/{demuxer.Duration:mm\\:ss} Rate={(currentPos - lastPos) / sw.Elapsed:0.0}x\r");
        lastPos = currentPos;
        sw.Restart();
    }
}

foreach (var transcoder in transcoders) {
    transcoder?.Flush();
    transcoder?.Dispose();
}


abstract class MediaTranscoder : IDisposable
{
    private MediaDecoder _decoder = null!;
    protected MediaStream _inStream = null!;

    private MediaMuxer _muxer = null!;
    private MediaStream _outStream = null!;
    protected MediaEncoder _encoder = null!;

    private MediaFrame _inFrame = null!;

    public MediaTranscoder(MediaMuxer muxer, MediaDemuxer demuxer, MediaStream inputStream)
    {
        _muxer = muxer;
        _inStream = inputStream;
        _decoder = demuxer.CreateStreamDecoder(inputStream, open: false);
        _decoder.SetThreadCount(0); //enable multi-threaded decoding
        _decoder.Open();

        _encoder = CreateEncoder(_decoder);
        _outStream = muxer.AddStream(_encoder);

        _inFrame = inputStream.Type switch {
            MediaTypes.Audio => new AudioFrame(),
            MediaTypes.Video => new VideoFrame()
        };
    }

    public void ReceivePacket(MediaPacket packet)
    {
        _decoder.SendPacket(packet);

        while (_decoder.ReceiveFrame(_inFrame)) {
            TranscodeFrame(_inFrame);
        }
    }

    protected abstract MediaEncoder CreateEncoder(MediaDecoder decoder);

    protected abstract void TranscodeFrame(MediaFrame inputFrame);

    protected void EncodeOutputFrame(MediaFrame? frame)
    {
        _muxer.EncodeAndWrite(_outStream, _encoder, frame);
    }

    public virtual void Flush()
    {
        EncodeOutputFrame(null);
    }

    public virtual void Dispose()
    {
        _decoder.Dispose();
        _encoder.Dispose();
        _inFrame.Dispose();
    }
}

class AudioTranscoder : MediaTranscoder
{
    private SwResampler _resampler = null!;
    private AudioFrame? _outFrame;

    public AudioTranscoder(MediaMuxer muxer, MediaDemuxer demuxer, MediaStream inputStream)
        : base(muxer, demuxer, inputStream) { }

    protected override MediaEncoder CreateEncoder(MediaDecoder mediaDecoder)
    {
        var decoder = (AudioDecoder)mediaDecoder;
        var format = new AudioFormat(SampleFormats.Float, 48000, 2);

        _resampler = new SwResampler(decoder.Format, format);

        return new AudioEncoder(CodecIds.Opus, format, bitrate: 128_000);
    }

    protected override void TranscodeFrame(MediaFrame? frame)
    {
        //Create frame lazily (after the encoder is open) so that we can use the proper frame size.
        _outFrame ??= new AudioFrame(_resampler.OutputFormat, ((AudioEncoder)_encoder).FrameSize ?? 4096) {
            Count = 0,
            PresentationTimestamp = 0
        };

        _resampler.SendFrame((AudioFrame?)frame);

        while (_resampler.ReceiveFrame(_outFrame)) {
            EncodeOutputFrame(_outFrame);
            _outFrame.PresentationTimestamp += _outFrame.Count;
        }
    }

    public override void Flush()
    {
        TranscodeFrame(null!); //Flush resampler before draining the encoder
        base.Flush();
    }

    public override void Dispose()
    {
        base.Dispose();
        _outFrame?.Dispose();
        _resampler.Dispose();
    }
}

class VideoTranscoder : MediaTranscoder
{
    private SwScaler _scaler = null!;
    private VideoFrame _outFrame = null!;

    public VideoTranscoder(MediaMuxer muxer, MediaDemuxer demuxer, MediaStream inputStream)
        : base(muxer, demuxer, inputStream) { }

    protected override MediaEncoder CreateEncoder(MediaDecoder mediaDecoder)
    {
        var decoder = (VideoDecoder)mediaDecoder;
        
        var format = decoder.FrameFormat.GetScaled(
            1280, 720, PixelFormats.YUV420P, keepAspectRatio: true,
            align: 2 //libx264 requires frame dimensions to be a multiple of 2.
        );
        _scaler = new SwScaler(decoder.FrameFormat, format);
        _outFrame = new VideoFrame(format);

        var encoder = new VideoEncoder(CodecIds.H264, format, _inStream.AvgFrameRate);
        encoder.SetOption("crf", "22");
        encoder.SetOption("preset", "veryfast");

        return encoder;
    }

    protected override void TranscodeFrame(MediaFrame frame)
    {
        _scaler.Convert((VideoFrame)frame, _outFrame);

        _outFrame.PresentationTimestamp = _encoder.GetFramePts(frame.BestEffortTimestamp!.Value, _inStream.TimeBase);
        EncodeOutputFrame(_outFrame);
    }

    public override void Dispose()
    {
        base.Dispose();

        _scaler.Dispose();
        _outFrame.Dispose();
    }
}