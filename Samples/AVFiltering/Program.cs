using System.Diagnostics;

using FFmpeg.AutoGen;
using FFmpeg.Wrapper;

if (args.Length < 2) {
    Console.WriteLine("Usage: AVFiltering <input path> <output path>");
    return;
}
using var demuxer = new MediaDemuxer(args[0]);
using var muxer = new MediaMuxer(args[1]);

using var videoStream = new StreamInfo(muxer, demuxer, MediaTypes.Video!);
using var audioStream = new StreamInfo(muxer, demuxer, MediaTypes.Audio!);

var videoDec = (VideoDecoder)videoStream.Decoder;
var audioDec = (AudioDecoder)audioStream.Decoder;
var scaledRes = videoDec.FrameFormat.GetScaled(1280, 720, keepAspectRatio: true, align: 2);

using var filter = MediaFilterPipeline.CreateBuilder()
    .VideoBufferSource(videoDec, ref scaledRes, autoRotate: true, name: "video_in")
    .AudioBufferSource(audioDec, name: "audio_in")
    .SwScale(scaledRes)
    .Split("audio_in", ["audio_in2", "a_out"])
    // Simple overlay with audio spectrum
    .Segment($@"[audio_in2] showspectrum=size={scaledRes.Width}x{scaledRes.Height / 5}  [audio_vis],
                [video_in] pad=height={scaledRes.Height * 6 / 5} [padded_video],
                [padded_video] [audio_vis] overlay=x=0:y={scaledRes.Height}, format=yuv420p")
    .Build();

videoStream.InitEncoder(filter.GetSink("out")!);
audioStream.InitEncoder(filter.GetSink("a_out")!);

muxer.Open();

using var packet = new MediaPacket();
var sw = Stopwatch.StartNew();
var lastPos = TimeSpan.Zero;

while (demuxer.Read(packet)) {
    // Dispatch processing to appropriate stream
    if (packet.StreamIndex == videoStream.InStream.Index) {
        videoStream.Process(packet, filter, "video_in", "out");

        // Early exit after 10s
        if (videoStream.InStream.GetTimestamp(packet.PresentationTimestamp!.Value).TotalSeconds >= 10) break;
    } else if (packet.StreamIndex == audioStream.InStream.Index) {
        audioStream.Process(packet, filter, "audio_in", "a_out");
    }

    // Show progress
    if (sw.ElapsedMilliseconds >= 1000) {
        var stream = demuxer.Streams[packet.StreamIndex];
        var currentPos = stream.GetTimestamp(packet.PresentationTimestamp!.Value);
        Console.WriteLine($"Progress: {currentPos:mm\\:ss}/{demuxer.Duration:mm\\:ss} Rate={(currentPos - lastPos) / sw.Elapsed:0.0}x\r");
        lastPos = currentPos;
        sw.Restart();
    }
}


class StreamInfo : IDisposable
{
    public MediaDecoder Decoder { get; }
    public MediaStream InStream { get; }

    private MediaMuxer _muxer;
    private MediaStream _outStream = null!;
    private MediaEncoder _encoder = null!;
    private MediaFrame _frame = null!;

    public StreamInfo(MediaMuxer muxer, MediaDemuxer demuxer, AVMediaType type)
    {
        _muxer = muxer;
        InStream = demuxer.FindBestStream(type)!;
        Decoder = demuxer.CreateStreamDecoder(InStream, open: false);
        Decoder.SetThreadCount(0); // enable multi-threaded decoding
        Decoder.Open();
    }

    public void InitEncoder(MediaBufferSink sink)
    {
        if (sink is VideoBufferSink vsink) {
            _frame = new VideoFrame();
            _encoder = new VideoEncoder(CodecIds.H264, vsink.Format, vsink.FrameRate);
        } else if (sink is AudioBufferSink asink) {
            _frame = new AudioFrame();
            _encoder = new AudioEncoder(CodecIds.AAC, asink.Format);
        }
        _encoder.TimeBase = Decoder.TimeBase;

        _outStream = _muxer.AddStream(_encoder);
    }

    public void Process(MediaPacket packet, MediaFilterPipeline filter, string inPort, string outPort)
    {
        Decoder.SendPacket(packet);

        // Filter decoded frames
        while (Decoder.ReceiveFrame(_frame)) {
            filter.SendFrame(_frame, inPort);
        }

        // Encode filtered frames
        while (filter.ReceiveFrame(_frame, outPort)) {
            _frame.PresentationTimestamp = _frame.BestEffortTimestamp;
            unsafe { _frame.Handle->pict_type = AVPictureType.AV_PICTURE_TYPE_NONE; }
            _muxer.EncodeAndWrite(_outStream, _encoder!, _frame);
        }
    }

    public virtual void Dispose()
    {
        _muxer.EncodeAndWrite(_outStream, _encoder, null); // flush encoder

        _frame.Dispose();
        _encoder.Dispose();
        Decoder.Dispose();
    }
}