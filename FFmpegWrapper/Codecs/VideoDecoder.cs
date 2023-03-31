namespace FFmpeg.Wrapper;

public unsafe class VideoDecoder : MediaDecoder
{
    public int Width => _ctx->width;
    public int Height => _ctx->height;
    public AVPixelFormat PixelFormat => _ctx->pix_fmt;

    public PictureFormat FrameFormat => new(Width, Height, PixelFormat);

    public VideoDecoder(AVCodecContext* ctx)
        : base(ctx, AVMediaType.AVMEDIA_TYPE_VIDEO) { }

    public VideoDecoder(AVCodecID codec)
        : base(codec, AVMediaType.AVMEDIA_TYPE_VIDEO) { }

    /// <summary> Allocates a frame suitable for use in <see cref="MediaDecoder.ReceiveFrame(MediaFrame)"/>. </summary>
    public VideoFrame AllocateFrame()
    {
        if (Width <= 0 || Height <= 0 || PixelFormat == AVPixelFormat.AV_PIX_FMT_NONE) {
            throw new InvalidOperationException("Invalid frame dimensions. (Is the decoder open?)");
        }
        return new VideoFrame(FrameFormat, clearToBlack: true);
    }
}