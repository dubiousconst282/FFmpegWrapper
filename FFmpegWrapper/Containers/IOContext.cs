using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Container
{
    public abstract unsafe class IOContext : IDisposable
    {
        protected const int SEEK_SET = 0;
        protected const int SEEK_CUR = 1;
        protected const int SEEK_END = 2;
        protected const int AVSEEK_SIZE = ffmpeg.AVSEEK_SIZE;

        private AVIOContext* _ctx;
        public AVIOContext* Context
        {
            get {
                ThrowIfDisposed();
                return _ctx;
            }
        }

        private GCHandle _readHandle, _writeHandle, _seekHandle;

        protected bool _disposed = false;

        public IOMode Mode { get; }

        public bool CanRead => Mode.HasFlag(IOMode.Read);
        public bool CanWrite => Mode.HasFlag(IOMode.Write);
        public bool CanSeek => _ctx->seekable != 0;

        public IOContext(int bufferSize, IOMode mode, bool canSeek)
        {
            Mode = mode;

            avio_alloc_context_read_packet read = null;
            avio_alloc_context_write_packet write = null;
            avio_alloc_context_seek seek = null;

            if (mode.HasFlag(IOMode.Read)) {
                read = Read;
                _readHandle = GCHandle.Alloc(read);
            }
            if (mode.HasFlag(IOMode.Write)) {
                write = Write;
                _writeHandle = GCHandle.Alloc(write);
            }
            if (canSeek) {
                seek = Seek;
                _seekHandle = GCHandle.Alloc(seek);
            }

            var buffer = (byte*)ffmpeg.av_mallocz((ulong)bufferSize);
            _ctx = ffmpeg.avio_alloc_context(buffer, bufferSize, mode.HasFlag(IOMode.Write) ? 1 : 0, null, read, write, seek);
            _ctx->seekable = canSeek ? ffmpeg.AVIO_SEEKABLE_NORMAL : 0;
        }

        /// <summary> Reads data from the underlying stream to the 'buf' parameter. </summary>
        /// <param name="opaque">Unused. Always null.</param>
        /// <param name="buf">The buffer containing the data to write to the underlying stream.</param>
        /// <param name="bufSize">The capacity of the 'buf' parameter. i.e.: the maximum number of bytes to read.</param>
        /// <returns>The number of bytes read, or a negative value for errors. </returns>
        protected abstract int Read(void* opaque, byte* buf, int bufSize);

        /// <summary> Writes data to the underlying stream. </summary>
        /// <param name="opaque">Unused. Always null.</param>
        /// <param name="buf">The buffer containing the data to write to the underlying stream.</param>
        /// <param name="bufSize">The number of bytes contained in the 'buf' parameter.</param>
        /// <returns>0 on success, or a negative value for errors. </returns>
        protected abstract int Write(void* opaque, byte* buf, int bufSize);

        /// <summary>Sets the position of the underlying stream.</summary>
        /// <param name="opaque">Unused. Always null.</param>
        /// <param name="offset">The new offset.</param>
        /// <param name="whence">Seek origin. May be one of SEEK_SET(0), SEEK_CUR(1), SEEK_END(2) or AVSEEK_SIZE</param>
        protected abstract long Seek(void* opaque, long offset, int whence);

        protected void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(IOContext));
            }
        }

        public virtual void Dispose()
        {
            if (!_disposed) {
                ffmpeg.av_free(_ctx->buffer);
                fixed (AVIOContext** c = &_ctx) ffmpeg.avio_context_free(c);

                if (_readHandle.IsAllocated) _readHandle.Free();
                if (_writeHandle.IsAllocated) _writeHandle.Free();
                if (_seekHandle.IsAllocated) _seekHandle.Free();

                _disposed = true;
            }
        }
    }
    [Flags]
    public enum IOMode
    {
        Read        = 0x01,
        Write       = 0x02,
        ReadWrite   = Read | Write
    }
}
