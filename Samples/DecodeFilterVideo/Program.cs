using FFmpeg.Wrapper;

// https://github.com/FFmpeg/FFmpeg/blob/master/doc/examples/decode_filter_video.c

if (args.Length < 2) {
    Console.WriteLine("Usage: DecodeFilterVideo <input path> <output path> <filter desc>?");
    return;
}

using var demuxer = new MediaDemuxer(args[0]);
using var muxer = new MediaMuxer(args[1]);
string filters = args.Length > 2 ? args[2] : "transpose=clock";

var inputStream = demuxer.FindBestStream(MediaTypes.Video)!;
using var decoder = (VideoDecoder)demuxer.CreateStreamDecoder(inputStream);

using var graph = new MediaFilterGraph();
//var sourceNode = graph.AddVideoBufferSource(decoder.FrameFormat, decoder.FrameRate, inputStream.TimeBase);
var sourceNode = graph.AddVideoBufferSource(decoder.FrameFormat, decoder.FrameRate, inputStream.TimeBase, decoder.Colorspace);
var parsedSegment = graph.Parse(filters, ("in", sourceNode.GetOutput(0)));
var sinkNode = graph.AddVideoBufferSink(parsedSegment["out"]);
graph.Configure();

var frameRate = demuxer.GuessFrameRate(inputStream);
using var encoder = new VideoEncoder(CodecIds.H264, sinkNode.Format, frameRate);
encoder.TimeBase = sinkNode.TimeBase;
var outputStream = muxer.AddStream(encoder);
muxer.Open();

using var packet = new MediaPacket();
using var srcFrame = new VideoFrame(decoder.FrameFormat);
using var dstFrame = new VideoFrame(encoder.FrameFormat);
var endTime = TimeSpan.FromSeconds(10);

while (demuxer.Read(packet)) {
    if (packet.StreamIndex != outputStream.Index) continue;
    if (inputStream.GetTimestamp(packet.PresentationTimestamp!.Value) >= endTime) break;
    decoder.SendPacket(packet);
    while (decoder.ReceiveFrame(srcFrame)) {
        sourceNode.SendFrame(srcFrame);
        using var frame = sinkNode.ReceiveFrame() ?? throw new Exception("Could not get frame from filter sink");
        muxer.EncodeAndWrite(outputStream, encoder, frame);
    }
}

//Flush frames delayed in the encoder
muxer.EncodeAndWrite(outputStream, encoder, null);