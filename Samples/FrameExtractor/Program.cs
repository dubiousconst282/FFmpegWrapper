using FFmpeg.Wrapper;

if (args.Length < 3) {
    Console.WriteLine("Usage: FrameExtractor <input video path> <output directory> <num frames>");
    return;
}
string inputPath = args[0];
string outDir = args[1];
int numFrames = int.Parse(args[2]);

Directory.CreateDirectory(outDir);

using var demuxer = new MediaDemuxer(inputPath);

var stream = demuxer.FindBestStream(MediaTypes.Video)!;
using var decoder = (VideoDecoder)demuxer.CreateStreamDecoder(stream);
using var packet = new MediaPacket();
using var frame = new VideoFrame();
using var filter = VideoFilterPipeline.CreateAutoRotate(stream);

for (int i = 0; i < numFrames; i++) {
    demuxer.Seek(demuxer.Duration!.Value * ((i + 0.5) / numFrames), SeekOptions.Forward);
    decoder.Flush(); //Discard any frames decoded before the seek

    //Read the file until we can decode a frame from the selected stream
    while (demuxer.Read(packet)) {
        if (packet.StreamIndex != stream.Index) continue; //Ignore packets from other streams

        decoder.SendPacket(packet);

        if (decoder.ReceiveFrame(frame)) {
            var ts = stream.GetTimestamp(frame.PresentationTimestamp!.Value);
            var fileName = $"{outDir}/{i}_{ts:hh\\.mm\\.ss}.jpg";
            if (filter is null) {
                frame.Save(fileName);
            } else {
                using var rotated = filter.Apply(frame);
                rotated.Save(fileName);
            }
            break;
        }
    }
}