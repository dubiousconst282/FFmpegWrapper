using FFmpeg.Wrapper;

if (args.Length < 3) {
    Console.WriteLine("Usage: ThumbExtractor <input video path> <output directory> <num frames>");
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

for (int i = 0; i < numFrames; i++) {
    var timestamp = demuxer.Duration * ((i + 0.5) / numFrames);
    demuxer.Seek(timestamp);
    decoder.Flush(); //Discard any frames decoded before the seek

    //Read the file until we can decode a frame for the selected stream
    while (demuxer.Read(packet)) {
        if (packet.StreamIndex != stream.Index) continue; //Ignore packets from other streams

        decoder.SendPacket(packet);
        
        if (decoder.ReceiveFrame(frame)) {
            var ts = TimeSpan.FromSeconds(frame.PresentationTimestamp!.Value * stream.TimeScale);
            Directory.CreateDirectory(outDir);
            frame.Save($"{outDir}/{i}_{ts:hh\\.mm\\.ss}.jpg");
            break;
        }
    }
}