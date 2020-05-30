using System;
using System.Linq;
using FFmpeg.AutoGen;
using FFmpegWrapper.Codec;

namespace FFmpegWrapper.Container
{
    public unsafe class MediaDemuxer : IDisposable
    {
        private AVFormatContext* _ctx; //be careful when using directly.
        private bool _disposed = false;
        private bool _leaveIoCtxOpen = true;

        public AVFormatContext* Context
        {
            get {
                ThrowIfDisposed();
                return _ctx;
            }
        }

        public string Filename { get; }
        public IOContext IOContext { get; }

        public TimeSpan Duration => TimeSpan.FromSeconds(Context->duration / (double)ffmpeg.AV_TIME_BASE);

        public MediaStream[] Streams { get; private set; }

        public bool CanSeek => IOContext == null ? true : IOContext.CanSeek;

        /// <summary> Force seeking to any (also non-key) frames. </summary>
        public bool SeekToAny
        {
            get => Context->seek2any != 0;
            set => Context->seek2any = value ? 1 : 0;
        }

        public MediaDemuxer(IOContext ioCtx, bool leaveOpen = true)
        {
            IOContext = ioCtx;
            _leaveIoCtxOpen = leaveOpen;
            _ctx = ffmpeg.avformat_alloc_context();
            if (_ctx == null) {
                throw new OutOfMemoryException("Could not allocate demuxer.");
            }

            _ctx->pb = ioCtx.Context;
            fixed (AVFormatContext** c = &_ctx) {
                ffmpeg.avformat_open_input(c, null, null, null).CheckError("Could not open input");
            }

            Initialize();
        }
        public MediaDemuxer(string filename)
        {
            Filename = filename;
            _ctx = ffmpeg.avformat_alloc_context();
            if (_ctx == null) {
                throw new OutOfMemoryException("Could not allocate demuxer");
            }
            fixed (AVFormatContext** c = &_ctx) {
                ffmpeg.avformat_open_input(c, filename, null, null).CheckError("Could not open input");
            }

            Initialize();
        }

        private void Initialize()
        {
            try {
                ffmpeg.avformat_find_stream_info(_ctx, null).CheckError("Could not find stream information");
                Streams = new MediaStream[_ctx->nb_streams];
                for (int i = 0; i < Streams.Length; i++) {
                    Streams[i] = new MediaStream(_ctx->streams[i], MediaStreamMode.Decode);
                }
            } catch {
                fixed (AVFormatContext** c = &_ctx) ffmpeg.avformat_close_input(c);
                throw;
            }
        }

        /// <summary> Returns the first stream of the specified type, or null if not found. </summary>
        public MediaStream FindStream(MediaType type)
        {
            return Streams.FirstOrDefault(s => s.Type == type);
        }

        public LavResult Read(AVPacket* packet)
        {
            return (LavResult)ffmpeg.av_read_frame(Context, packet);
        }
        public LavResult Read(MediaPacket packet)
        {
            var pkt = new AVPacket();
            try {
                LavResult result = Read(&pkt);

                if (result.IsSuccess()) {
                    packet.SetData(&pkt);
                }

                return result;
            } finally {
                ffmpeg.av_packet_unref(&pkt);
            }
        }

        public void Seek(TimeSpan timestamp)
        {
            if (!CanSeek) {
                throw new InvalidOperationException("Backing IO context is not seekable.");
            }

            AVRational timebase = Streams[0].Stream->time_base;

            long frame = (long)Math.Round(timestamp.TotalSeconds * timebase.den / timebase.num);

            ffmpeg.avformat_seek_file(Context, 0, 0, frame, frame, ffmpeg.AVSEEK_FLAG_FRAME).CheckError("Seek failed.");

            for (int i = 0; i < Streams.Length; i++) {
                Streams[i].Codec?.Flush();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(MediaDemuxer));
            }
        }

        public void Dispose()
        {
            if (!_disposed) {
                fixed (AVFormatContext** c = &_ctx) ffmpeg.avformat_close_input(c);

                if (IOContext != null && !_leaveIoCtxOpen) {
                    IOContext.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
