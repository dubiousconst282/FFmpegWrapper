using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public unsafe abstract class CodecBase : IDisposable
    {
        private AVCodec* _codec;
        private AVCodecContext* _ctx; //be careful when using directly.
        protected bool _disposed = false;
        protected bool _ownContext;
        private bool _userExtraData = false;

        public AVCodecContext* Context
        {
            get {
                ThrowIfDisposed();
                return _ctx;
            }
        }
        public AVCodec* Codec
        {
            get {
                ThrowIfDisposed();
                return _codec;
            }
        }

        public bool IsOpen { get; private set; } = false;

        public string CodecName => new string((sbyte*)_codec->long_name);

        public AVRational TimeBase
        {
            get => Context->time_base;
            set => SetOrThrowIfOpen(ref Context->time_base, value);
        }
        public AVRational FrameRate
        {
            get => Context->framerate;
            set => SetOrThrowIfOpen(ref Context->framerate, value);
        }
        /// <summary> Timestamp scale, in seconds. </summary>
        public double TimeScale => ffmpeg.av_q2d(Context->time_base);

        public Span<byte> ExtraData
        {
            get => GetExtraData();
            set => SetExtraData(value);
        }

        /// <summary>
        /// Indicates if the encoder or decoder requires flushing with NULL input at the end in order to give the complete and correct output.
        /// Equivalent to: <code>(Codec->capabilities &amp; ffmpeg.AV_CODEC_CAP_DELAY) != 0</code>
        /// </summary>
        public bool Delayed => (Codec->capabilities & ffmpeg.AV_CODEC_CAP_DELAY) != 0;

        public MediaType CodecType => (MediaType)Context->codec_type;

        public CodecBase(AVCodec* codec)
        {
            _ownContext = true;
            _codec = codec;
            _ctx = ffmpeg.avcodec_alloc_context3(_codec);
            if (_ctx == null) {
                throw new OutOfMemoryException("Failed to allocate codec context.");
            }
        }
        public CodecBase(AVCodecContext* ctx)
        {
            _ownContext = false;
            _ctx = ctx;
            _codec = ctx->codec;
        }

        /// <summary> Initializes the codec. </summary>
        public virtual void Open()
        {
            if (!IsOpen) {
                ffmpeg.avcodec_open2(Context, _codec, null).CheckError("Could not open codec");
                IsOpen = true;
            }
        }

        /// <summary> Reset the decoder state / flush internal buffers. </summary>
        public virtual void Flush()
        {
            if (IsOpen) {
                ffmpeg.avcodec_flush_buffers(Context);
            }
        }

        private Span<byte> GetExtraData()
        {
            var ctx = Context;

            if (ctx->extradata != null) {
                return new Span<byte>(ctx->extradata, ctx->extradata_size);
            }
            return default;
        }
        private void SetExtraData(Span<byte> buf)
        {
            ThrowIfOpen();

            var ctx = Context;

            ffmpeg.av_freep(&ctx->extradata);

            if (buf != default) {
                ctx->extradata = (byte*)ffmpeg.av_mallocz((ulong)buf.Length + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE);
                ctx->extradata_size = buf.Length;
                buf.CopyTo(new Span<byte>(ctx->extradata, buf.Length));
            } else {
                ctx->extradata = null;
                ctx->extradata_size = 0;
            }
            _userExtraData = true;
        }

        protected void SetOrThrowIfOpen<T>(ref T loc, T value)
        {
            ThrowIfOpen();
            loc = value;
        }

        protected void ThrowIfOpen()
        {
            if (IsOpen) {
                throw new InvalidOperationException("Value must be set before the codec is open.");
            }
        }
        protected void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(CodecBase));
            }
        }

        public virtual void Dispose()
        {
            if (!_disposed) {
                _disposed = true;

                if (_userExtraData) {
                    ffmpeg.av_freep(&_ctx->extradata);
                }
                if (_ownContext) {
                    fixed (AVCodecContext** c = &_ctx) ffmpeg.avcodec_free_context(c);
                }
            }
        }
    }
}
