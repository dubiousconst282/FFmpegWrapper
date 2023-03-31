namespace FFmpeg.Wrapper;

public unsafe class SwScaler : FFObject
{
    private SwsContext* _ctx;

    public SwsContext* Handle {
        get {
            ThrowIfDisposed();
            return _ctx;
        }
    }

    public PictureFormat InputFormat { get; }
    public PictureFormat OutputFormat { get; }

    public SwScaler(PictureFormat inFmt, PictureFormat outFmt, InterpolationMode flags = InterpolationMode.Bicubic)
    {
        InputFormat = inFmt;
        OutputFormat = outFmt;

        _ctx = ffmpeg.sws_getContext(inFmt.Width, inFmt.Height, inFmt.PixelFormat,
                                     outFmt.Width, outFmt.Height, outFmt.PixelFormat,
                                     (int)flags, null, null, null);
    }
    public void Convert(VideoFrame src, VideoFrame dst)
    {
        Convert(src.Handle, dst.Handle);
    }
    public void Convert(AVFrame* src, AVFrame* dst)
    {
        var srcFmt = InputFormat;
        var dstFmt = OutputFormat;

        if ((src->format != (int)srcFmt.PixelFormat || src->width != srcFmt.Width || src->height != srcFmt.Height) ||
            (dst->format != (int)dstFmt.PixelFormat || dst->width != dstFmt.Width || dst->height != dstFmt.Height)
        ) {
            throw new ArgumentException("Frame must match rescaler formats");
        }
        ffmpeg.sws_scale(Handle, src->data, src->linesize, 0, InputFormat.Height, dst->data, dst->linesize);
    }
    /// <summary> Converts and rescale interleaved pixel data into the destination format.</summary>
    public void Convert(ReadOnlySpan<byte> src, int stride, VideoFrame dst)
    {
        var srcFmt = InputFormat;
        var dstFmt = OutputFormat;

        if ((srcFmt.IsPlanar || src.Length < srcFmt.Height * stride) ||
            (dst.PixelFormat != dstFmt.PixelFormat || dst.Width != dstFmt.Width || dst.Height != dstFmt.Height)
        ) {
            throw new ArgumentException("Frame must match rescaler formats");
        }
        fixed (byte* pSrc = src) {
            ffmpeg.sws_scale(Handle, new[] { pSrc }, new[] { stride }, 0, dst.Height, dst.Handle->data, dst.Handle->linesize);
        }
    }

    protected override void Free()
    {
        if (_ctx != null) {
            ffmpeg.sws_freeContext(_ctx);
            _ctx = null;
        }
    }
    private void ThrowIfDisposed()
    {
        if (_ctx == null) {
            throw new ObjectDisposedException(nameof(SwScaler));
        }
    }
}
public enum InterpolationMode
{
    FastBilinear    = ffmpeg.SWS_FAST_BILINEAR,
    Bilinear        = ffmpeg.SWS_BILINEAR,
    Bicubic         = ffmpeg.SWS_BICUBIC,
    NearestNeighbor = ffmpeg.SWS_POINT,
    Box             = ffmpeg.SWS_AREA,
    Gaussian        = ffmpeg.SWS_GAUSS,
    Sinc            = ffmpeg.SWS_SINC,
    Lanczos         = ffmpeg.SWS_LANCZOS,
    Spline          = ffmpeg.SWS_SPLINE
}
