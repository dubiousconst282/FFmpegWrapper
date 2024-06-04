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
        if (_ctx == null) {
            throw new OutOfMemoryException();
        }
    }

    /// <summary> Sets the input and output matrices to be used for YUV conversion. </summary>
    /// <remarks> Color transfer functions are ignored by swscale. Use the colorspace filter instead. </remarks>
    public void SetColorspace(in PictureColorspace input, in PictureColorspace output)
    {
        ThrowIfDisposed();
        int* table, invTable;
        int srcRange, dstRange, brightness, contrast, saturation;
        ffmpeg.sws_getColorspaceDetails(_ctx, &invTable, &srcRange, &table, &dstRange, &brightness, &contrast, &saturation);

        table = ffmpeg.sws_getCoefficients((int)input.Matrix);
        invTable = ffmpeg.sws_getCoefficients((int)output.Matrix);

        if (input.Range != AVColorRange.AVCOL_RANGE_UNSPECIFIED) {
            srcRange = input.Range == AVColorRange.AVCOL_RANGE_JPEG ? 1 : 0;
        }
        if (output.Range != AVColorRange.AVCOL_RANGE_UNSPECIFIED) {
            dstRange = output.Range == AVColorRange.AVCOL_RANGE_JPEG ? 1 : 0;
        }

        ffmpeg.sws_setColorspaceDetails(_ctx, in *(int_array4*)invTable, srcRange, in *(int_array4*)table, dstRange, brightness, contrast, saturation);
    }

    public void Convert(VideoFrame src, VideoFrame dst)
    {
        Convert(src.Handle, dst.Handle);
    }
    public void Convert(AVFrame* src, AVFrame* dst)
    {
        CheckFrame(src, InputFormat, input: true);
        CheckFrame(dst, OutputFormat, input: false);
        ffmpeg.sws_scale_frame(_ctx, dst, src).CheckError();
    }

    /// <summary> Converts and rescales <paramref name="src"/> into the given frame. The input pixel format must be interleaved. </summary>
    /// <param name="stride"> The number of bytes per pixel line in <paramref name="src"/>. </param>
    public void Convert(ReadOnlySpan<byte> src, int stride, VideoFrame dst)
    {
        CheckBuffer(src, stride, InputFormat, input: true);
        CheckFrame(dst.Handle, OutputFormat, input: false);

        fixed (byte* pSrc = src) {
            ffmpeg.sws_scale(Handle, new[] { pSrc }, new[] { stride }, 0, InputFormat.Height, dst.Handle->data, dst.Handle->linesize).CheckError();
        }
    }

    /// <summary> Converts and rescales <paramref name="src"/> into the given buffer. The output pixel format must be interleaved. </summary>
    /// <param name="stride"> The number of bytes per pixel line in <paramref name="dst"/>. </param>
    public void Convert(VideoFrame src, Span<byte> dst, int stride)
    {
        CheckFrame(src.Handle, InputFormat, input: true);
        CheckBuffer(dst, stride, OutputFormat, input: false);
        
        fixed (byte* pDst = dst) {
            ffmpeg.sws_scale(Handle, src.Handle->data, src.Handle->linesize, 0, src.Height, new[] { pDst }, new[] { stride }).CheckError();
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
            ffmpeg.sws_scale(Handle, new[] { pSrc }, new[] { srcStride }, 0, InputFormat.Height, new[] { pDst }, new[] { dstStride }).CheckError();
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

[Flags]
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
    Spline          = ffmpeg.SWS_SPLINE,

    /// <summary> Flag: Prioritize quality over speed. This sets ACCURATE_RND, BITEXACT, and FULL_CHR_H_INT. </summary>
    /// <remarks> See https://stackoverflow.com/a/70894724 for details on the meaning of these flags. </remarks>
    HighQuality     = ffmpeg.SWS_ACCURATE_RND | ffmpeg.SWS_BITEXACT | ffmpeg.SWS_FULL_CHR_H_INT,

    /// <summary> Flag: Always interpolate chroma channels when upsampling. </summary>
    InterpolateChroma = ffmpeg.SWS_FULL_CHR_H_INT,
}