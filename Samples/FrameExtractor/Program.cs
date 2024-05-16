using FFmpeg.Wrapper;

using FrameExtractor;

if (args.Length < 3) {
    Console.WriteLine("Usage: FrameExtractor <input video path> <output directory> <num frames>");
    return;
}
string inputPath = args[0];
string outDir = args[1];
int numFrames = int.Parse(args[2]);

using var demuxer = new MediaDemuxer(inputPath);

var stream = demuxer.FindBestStream(MediaTypes.Video)!;
using var decoder = (VideoDecoder)demuxer.CreateStreamDecoder(stream);
using var packet = new MediaPacket();
using var frame = new VideoFrame();
using var filter = AutoRotator.Create(stream, decoder);

for (int i = 0; i < numFrames; i++) {
    demuxer.Seek(demuxer.Duration!.Value * ((i + 0.5) / numFrames), SeekOptions.Forward);
    decoder.Flush(); //Discard any frames decoded before the seek

    //Read the file until we can decode a frame from the selected stream
    while (demuxer.Read(packet)) {
        if (packet.StreamIndex != stream.Index) continue; //Ignore packets from other streams

        decoder.SendPacket(packet);

        if (decoder.ReceiveFrame(frame)) {
            var ts = stream.GetTimestamp(frame.PresentationTimestamp!.Value);
            Directory.CreateDirectory(outDir);
            var fileName = $"{outDir}/{i}_{ts:hh\\.mm\\.ss}.jpg";
            if (filter is null) {
                frame.Save(fileName);
            } else {
                using var rotated = filter.Convert(frame);
                rotated.Save(fileName);
            }
            break;
        }
    }
}

class AutoRotator : IDisposable
{
    private readonly MediaFilterGraph _graph;
    private readonly MediaBufferSource _source;
    private readonly VideoBufferSink _sink;

    public MediaBufferSource Source => _source;

    public VideoBufferSink Sink => _sink;

    public static AutoRotator? Create(MediaStream stream, VideoDecoder decoder)
    {
        var filters = stream.CodecPars.GetAutoRotateFilterDescription();
        return filters is null ? null : new AutoRotator(stream, decoder, filters);
    }

    private AutoRotator(MediaStream stream, VideoDecoder decoder, string parsedFilters)
    {
        _graph = new MediaFilterGraph();
        _source = _graph.AddVideoBufferSource(decoder.FrameFormat, decoder.Colorspace, stream.TimeBase, decoder.FrameRate);
        var parsedSegment = _graph.Parse(parsedFilters, ("in", _source.GetOutput(0)));
        _sink = _graph.AddVideoBufferSink(parsedSegment["out"]);
        _graph.Configure();
    }

    public VideoFrame Convert(VideoFrame srcFrame)
    {
        _source.SendFrame(srcFrame);
        return _sink.ReceiveFrame() ?? throw new Exception("Could not get frame from filter sink");
    }

    public void Dispose()
    {
        _graph.Dispose();
    }
}