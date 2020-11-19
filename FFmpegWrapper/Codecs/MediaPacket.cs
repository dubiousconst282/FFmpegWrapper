using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public class MediaPacket
    {
        private byte[] _buf;
        private const long NO_PTS = unchecked((long)0x8000000000000000);

        public bool IsEmpty { get; private set; }

        public long? PresentationTimestamp { get; private set; }
        public long? DecompressionTimestamp { get; private set; }

        public long Duration { get; private set; }
        public int StreamIndex { get; private set; }
        public Memory<byte> Data { get; private set; }

        public unsafe MediaPacket(int initialCapacity = 0)
        {
            _buf = initialCapacity <= 0 ? null : new byte[initialCapacity];
        }

        public unsafe void SetData(AVPacket* pkt)
        {
            long? pts = pkt->pts;
            long? dts = pkt->dts;

            if (pkt->pts == NO_PTS) pts = null;
            if (pkt->dts == NO_PTS) dts = null;

            SetData(pts, dts, pkt->duration, pkt->stream_index, new ReadOnlySpan<byte>(pkt->data, pkt->size));
        }
        public void SetData(long? pts, long? dts, long dur, int streamIndex, ReadOnlySpan<byte> data)
        {
            IsEmpty = false;

            PresentationTimestamp = pts;
            DecompressionTimestamp = dts;
            Duration = dur;
            StreamIndex = streamIndex;

            if (_buf == null || _buf.Length < data.Length) {
                _buf = new byte[data.Length + 64];
            }

            Data = new Memory<byte>(_buf, 0, data.Length);
            data.CopyTo(Data.Span);
        }
        public void SetDataNoCopy(long? pts, long? dts, long dur, int streamIndex, Memory<byte> data)
        {
            IsEmpty = false;

            PresentationTimestamp = pts;
            DecompressionTimestamp = dts;

            Duration = dur;
            StreamIndex = streamIndex;
            Data = data;
        }

        public void Clear()
        {
            IsEmpty = true;
            PresentationTimestamp = null;
            DecompressionTimestamp = null;
            Duration = 0;
            StreamIndex = 0;
            Data = default;
        }
    }
}
