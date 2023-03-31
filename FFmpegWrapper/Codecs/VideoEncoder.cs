namespace FFmpeg.Wrapper;

public unsafe class VideoEncoder : MediaEncoder
{
    public int Width {
        get => _ctx->width;
        set => SetOrThrowIfOpen(ref _ctx->width, value);
    }
    public int Height {
        get => _ctx->height;
        set => SetOrThrowIfOpen(ref _ctx->height, value);
    }
    public AVPixelFormat PixelFormat {
        get => _ctx->pix_fmt;
        set => SetOrThrowIfOpen(ref _ctx->pix_fmt, value);
    }

    public PictureFormat FrameFormat {
        get => new(Width, Height, PixelFormat);
        set {
            _ctx->width = value.Width;
            _ctx->height = value.Height;
            _ctx->pix_fmt = value.PixelFormat;
        }
    }

    public int GopSize {
        get => _ctx->gop_size;
        set => SetOrThrowIfOpen(ref _ctx->gop_size, value);
    }
    public int MaxBFrames {
        get => _ctx->max_b_frames;
        set => SetOrThrowIfOpen(ref _ctx->max_b_frames, value);
    }

    public int MinQuantizer {
        get => _ctx->qmin;
        set => SetOrThrowIfOpen(ref _ctx->qmin, value);
    }
    public int MaxQuantizer {
        get => _ctx->qmax;
        set => SetOrThrowIfOpen(ref _ctx->qmax, value);
    }

    public int CompressionLevel {
        get => _ctx->compression_level;
        set => SetOrThrowIfOpen(ref _ctx->compression_level, value);
    }

    public ReadOnlySpan<AVPixelFormat> SupportedPixelFormats
        => Helpers.GetSpanFromSentinelTerminatedPtr(_ctx->codec->pix_fmts, AVPixelFormat.AV_PIX_FMT_NONE);

    public VideoEncoder(AVCodecID codec)
        : base(codec, AVMediaType.AVMEDIA_TYPE_VIDEO) { }

    public VideoEncoder(AVCodecID codec, PictureFormat frameFmt, double frameRate, int bitrate)
        : this(codec)
    {
        FrameFormat = frameFmt;
        FrameRate = ffmpeg.av_d2q(frameRate, 10000);
        TimeBase = ffmpeg.av_inv_q(FrameRate);
        BitRate = bitrate;
    }
}