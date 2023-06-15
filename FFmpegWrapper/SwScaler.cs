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
        CheckFrame(src, InputFormat, input: true);
        CheckFrame(dst, OutputFormat, input: false);
        ffmpeg.sws_scale(Handle, src->data, src->linesize, 0, src->height, dst->data, dst->linesize);
    }

    /// <summary> Converts and rescales <paramref name="src"/> into the given frame. The input pixel format must be interleaved. </summary>
    /// <param name="stride"> The number of bytes per pixel line in <paramref name="src"/>. </param>
    public void Convert(ReadOnlySpan<byte> src, int stride, VideoFrame dst)
    {
        CheckBuffer(src, stride, InputFormat, input: true);
        CheckFrame(dst.Handle, OutputFormat, input: false);

        fixed (byte* pSrc = src) {
            ffmpeg.sws_scale(Handle, new[] { pSrc }, new[] { stride }, 0, dst.Height, dst.Handle->data, dst.Handle->linesize);
        }
    }

    /// <summary> Converts and rescales <paramref name="src"/> into the given buffer. The output pixel format must be interleaved. </summary>
    /// <param name="stride"> The number of bytes per pixel line in <paramref name="dst"/>. </param>
    public void Convert(VideoFrame src, Span<byte> dst, int stride)
    {
        CheckFrame(src.Handle, InputFormat, input: true);
        CheckBuffer(dst, stride, OutputFormat, input: false);
        
        fixed (byte* pDst = dst) {
            ffmpeg.sws_scale(Handle, src.Handle->data, src.Handle->linesize, 0, src.Height, new[] { pDst }, new[] { stride });
        }
    }

    /// <summary> Converts and rescales <paramref name="src"/> into the given buffer. The input and output pixel formats must be interleaved. </summary>
    /// <param name="srcStride"> The number of bytes per pixel line in <paramref name="src"/>. </param>
    /// <param name="dstStride"> The number of bytes per pixel line in <paramref name="dst"/>. </param>
    public void Convert(ReadOnlySpan<byte> src, int srcStride, Span<byte> dst, int dstStride)
    {
        CheckBuffer(src, srcStride, InputFormat, input: true);
        CheckBuffer(dst, dstStride, OutputFormat, input: false);

        fixed (byte* pSrc = src)
        fixed (byte* pDst = dst) {
            ffmpeg.sws_scale(Handle, new[] { pSrc }, new[] { srcStride }, 0, InputFormat.Height, new[] { pDst }, new[] { dstStride });
        }
    }

    private static void CheckFrame(AVFrame* frame, in PictureFormat format, bool input)
    {
        if (frame->format != (int)format.PixelFormat || frame->width != format.Width || frame->height != format.Height) {
            throw new ArgumentException((input ? "Input" : "Output") + " frame must match rescaler format");
        }
    }

    private static void CheckBuffer(ReadOnlySpan<byte> buffer, int stride, in PictureFormat format, bool input)
    {
        if (format.IsPlanar || buffer.Length < (long)format.Height * stride || 
            stride < ffmpeg.av_image_get_linesize(format.PixelFormat, format.Width, 0)
        ) {
            throw new ArgumentException((input ? "Input" : "Output") + " buffer must match rescaler format");
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
