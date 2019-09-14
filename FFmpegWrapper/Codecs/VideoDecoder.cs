using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public unsafe class VideoDecoder : MediaDecoder
    {
        public int Width
        {
            get => Context->width;
            set => SetOrThrowIfOpen(ref Context->width, value);
        }
        public int Height
        {
            get => Context->height;
            set => SetOrThrowIfOpen(ref Context->height, value);
        }
        public AVPixelFormat PixelFormat
        {
            get => Context->pix_fmt;
            set => SetOrThrowIfOpen(ref Context->pix_fmt, value);
        }

        public PictureInfo Info
        {
            get => new PictureInfo(Width, Height, PixelFormat);
            set {
                ThrowIfOpen();
                Context->width = value.Width;
                Context->height = value.Height;
                Context->pix_fmt = value.PixelFormat;
            }
        }

        public VideoDecoder(AVCodecContext* ctx) : base(ctx, AVMediaType.AVMEDIA_TYPE_VIDEO)
        {
        }
        public VideoDecoder(AVCodecID codec) : base(codec, AVMediaType.AVMEDIA_TYPE_VIDEO)
        {
        }

        public Picture AllocateFrame()
        {
            if (Width <= 0 || Height <= 0 || PixelFormat == AVPixelFormat.AV_PIX_FMT_NONE) {
                throw new InvalidOperationException("Invalid picture format. (Is the decoder open?)");
            }

            return new Picture(Width, Height, PixelFormat, true);
        }

        public LavResult ReceiveFrame(Picture pic, out long timestamp)
        {
            if (pic == null) throw new ArgumentNullException();
            timestamp = 0;

            var frame = pic.Frame;
            var result = ReceiveFrame(frame);
            if (result.IsSuccess()) {
                timestamp = frame->pts;
            }
            return result;
        }
    }
}
