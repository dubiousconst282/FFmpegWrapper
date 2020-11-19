using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public unsafe abstract class MediaFrame : IDisposable
    {
        private const long NO_PTS = unchecked((long)0x8000000000000000);

        protected AVFrame* _frame;
        protected bool _ownFrame = true;
        protected bool _disposed = false;

        public AVFrame* Frame
        {
            get {
                ThrowIfDisposed();
                return _frame;
            }
        }

        public long? BestEffortTimestamp
        {
            get => GetTimestamp(Frame->best_effort_timestamp);
            //set => SetTimestamp(ref Frame->best_effort_timestamp, value);
        }
        public long? PresentationTimestamp
        {
            get => GetTimestamp(Frame->pts);
            set => SetTimestamp(ref Frame->pts, value);
        }

        protected static long? GetTimestamp(long ts) => ts == NO_PTS ? default(long?) : ts;
        protected static void SetTimestamp(ref long ts, long? v) => ts = v == null ? NO_PTS : v.Value;

        public void Dispose()
        {
            if (!_disposed) {
                if (_ownFrame) { fixed (AVFrame** ppFrame = &_frame) ffmpeg.av_frame_free(ppFrame); }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
        protected void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
