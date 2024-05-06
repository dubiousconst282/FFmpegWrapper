// https://github.com/FFmpeg/FFmpeg/blob/master/doc/examples/remux.c

using FFmpeg.Wrapper;

if (args.Length < 2) {
    Console.WriteLine("Usage: Remux <input path> <output path.mkv>");
    return;
}

using var demuxer = new MediaDemuxer(args[0]);
using var remuxer = new MediaMuxer(args[1]);

var srcStreams = demuxer.Streams.OrderBy(s => s.Index).ToArray();
var dstStreams = new MediaStream[srcStreams.Length];
foreach (var srcStream in srcStreams) {
    dstStreams[srcStream.Index] = remuxer.AddStream(srcStream);
}
remuxer.Open();

using var packet = new MediaPacket();
unsafe {
    while (demuxer.Read(packet)) {
        var index = packet.StreamIndex;
        packet.RescaleTS(srcStreams[index].TimeBase, dstStreams[index].TimeBase);
        packet.Handle->pos = -1;
        remuxer.Write(packet);
    }
}