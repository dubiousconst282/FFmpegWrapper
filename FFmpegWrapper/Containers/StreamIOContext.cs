using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Container
{
    /// <summary>
    /// Wraps a <see cref="Stream"/> into a AVIOContext.
    /// </summary>
    public class StreamIOContext : IOContext
    {
        public Stream BaseStream { get; }

        private byte[] _buffer;
        private bool _leaveOpen;

        public StreamIOContext(Stream s, IOMode mode, bool leaveOpen = false)
            : base(4096, mode, s.CanSeek)
        {
            _buffer = new byte[4096];
            BaseStream = s;
            _leaveOpen = leaveOpen;
        }

        protected override unsafe int Read(void* opaque, byte* buf, int bufSize)
        {
            try {
                int bytes = BaseStream.Read(_buffer, 0, Math.Min(bufSize, _buffer.Length));

                if (bytes == 0) {
                    return ffmpeg.AVERROR_EOF;
                }
                Marshal.Copy(_buffer, 0, (IntPtr)buf, bytes);
                return bytes;
            } catch {
                return ffmpeg.AVERROR_UNKNOWN;
            }
        }
        protected override unsafe int Write(void* opaque, byte* buf, int bufSize)
        {
            try {
                while (bufSize > 0) {
                    int size = Math.Min(_buffer.Length, bufSize);
                    Marshal.Copy((IntPtr)buf, _buffer, 0, size);

                    BaseStream.Write(_buffer, 0, size);

                    buf += size;
                    bufSize -= size;
                }
                return 0;
            } catch {
                return ffmpeg.AVERROR_UNKNOWN;
            }
        }
        protected override unsafe long Seek(void* opaque, long offset, int whence)
        {
            SeekOrigin origin;
            switch (whence) {
                case SEEK_SET: origin = SeekOrigin.Begin; break;
                case SEEK_CUR: origin = SeekOrigin.Current; break;
                case SEEK_END: origin = SeekOrigin.End; break;
                case AVSEEK_SIZE: return BaseStream.Length;
                default: return -22; //EINVAL
            }
            return BaseStream.Seek(offset, origin);
        }

        public override void Dispose()
        {
            if (!_disposed && !_leaveOpen) {
                BaseStream.Dispose();
            }
            base.Dispose();
        }
    }
}
