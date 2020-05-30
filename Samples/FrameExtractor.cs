using FFmpegWrapper;
using FFmpegWrapper.Codec;
using FFmpegWrapper.Container;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Samples
{
    public class FrameExtractor : IDisposable
    {
        private MediaDemuxer demuxer;
        private MediaStream stream;
        private VideoDecoder decoder;

        public FrameExtractor(string filename)
        {
            demuxer = new MediaDemuxer(filename);
            stream = demuxer.FindStream(MediaType.Video);
            decoder = (VideoDecoder)stream.OpenDecoder();
        }

        public void Run()
        {
            Directory.CreateDirectory("frames/");

            var pkt = new MediaPacket();
            int frameNum = 0;

            using (var frame = decoder.AllocateFrame()) {
                while (demuxer.Read(pkt).IsSuccess()) {
                    if (pkt.StreamIndex != stream.Index) {
                        continue;
                    }
                    decoder.SendPacket(pkt);

                    while (decoder.ReceiveFrame(frame).IsSuccess()) {
                        var ts = TimeSpan.FromSeconds(frame.PresentationTimestamp.Value * stream.TimeScale);
                        frame.Save($"frames/{frameNum++}.jpg");
                    }
                }
            }
        }

        public void Dispose()
        {
            demuxer.Dispose();
        }
    }
}
