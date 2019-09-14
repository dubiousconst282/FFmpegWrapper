using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public unsafe class VideoEncoder : MediaEncoder
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
                Context->width = value.Width;
                Context->height = value.Height;
                Context->pix_fmt = value.PixelFormat;
            }
        }

        public int GopSize
        {
            get => Context->gop_size;
            set => SetOrThrowIfOpen(ref Context->gop_size, value);
        }
        public int MaxBFrames
        {
            get => Context->max_b_frames;
            set => SetOrThrowIfOpen(ref Context->max_b_frames, value);
        }

        public int MinQuantizer
        {
            get => Context->qmin;
            set => SetOrThrowIfOpen(ref Context->qmin, value);
        }
        public int MaxQuantizer
        {
            get => Context->qmax;
            set => SetOrThrowIfOpen(ref Context->qmax, value);
        }

        public int CompressionLevel
        {
            get => Context->compression_level;
            set => SetOrThrowIfOpen(ref Context->compression_level, value);
        }

        public AVPixelFormat[] SupportedPixelFormats => FFmpegHelpers.ToArray(Codec->pix_fmts, AVPixelFormat.AV_PIX_FMT_NONE);

        public VideoEncoder(AVCodecID codec)
            : base(codec, AVMediaType.AVMEDIA_TYPE_VIDEO)
        {
            Width = 854;
            Height = 480;
            PixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;
            TimeBase = new AVRational() { den = 25000, num = 1000 };
            FrameRate = new AVRational() { den = 1000, num = 25000 };
            BitRate = 768 * 1024;
        }

        public VideoEncoder(AVCodecID codec, PictureInfo info, double fps, int bitrate)
            : this(codec, info.Width, info.Height, info.PixelFormat, fps, bitrate)
        {

        }

        public VideoEncoder(AVCodecID codec, int w, int h, AVPixelFormat fmt, double fps, int bitrate)
            : base(codec, AVMediaType.AVMEDIA_TYPE_VIDEO)
        {
            Width = w;
            Height = h;
            PixelFormat = fmt;
            TimeBase = new AVRational() { den = (int)Math.Round(fps * 1000), num = 1000 };
            FrameRate = new AVRational() { den = 1000, num = (int)Math.Round(fps * 1000) };
            BitRate = bitrate;
        }

        public LavResult SendFrame(Picture pic, long timestamp)
        {
            if (pic != null) {
                if (pic.Width != Width || pic.Height != Height) {
                    throw new ArgumentException("Picture must have the resolution same as the encoder.");
                }
                AVFrame* frame = pic.Frame;
                frame->pts = timestamp;
                return SendFrame(frame);
            } else {
                return SendFrame(null);
            }
        }
    }
}
