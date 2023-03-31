namespace FFmpeg.Wrapper;

public unsafe class SwResampler : FFObject
{
    private SwrContext* _ctx;

    public SwrContext* Context {
        get {
            ThrowIfDisposed();
            return _ctx;
        }
    }

    public AudioFormat InputFormat { get; }
    public AudioFormat OutputFormat { get; }

    /// <summary>
    /// Gets the number of dst buffered samples
    /// </summary>
    public int BufferedSamples => (int)ffmpeg.swr_get_delay(_ctx, OutputFormat.SampleRate);

    public SwResampler(AudioFormat inFmt, AudioFormat outFmt)
    {
        _ctx = ffmpeg.swr_alloc();

        var tempLayout = inFmt.Layout;
        ffmpeg.av_opt_set_chlayout(_ctx, "in_chlayout", &tempLayout, 0);
        ffmpeg.av_opt_set_int(_ctx, "in_sample_rate", inFmt.SampleRate, 0);
        ffmpeg.av_opt_set_int(_ctx, "in_sample_fmt", (long)inFmt.SampleFormat, 0);

        tempLayout = outFmt.Layout;
        ffmpeg.av_opt_set_chlayout(_ctx, "out_chlayout", &tempLayout, 0);
        ffmpeg.av_opt_set_int(_ctx, "out_sample_rate", outFmt.SampleRate, 0);
        ffmpeg.av_opt_set_int(_ctx, "out_sample_fmt", (long)outFmt.SampleFormat, 0);

        ffmpeg.swr_init(_ctx);

        InputFormat = inFmt;
        OutputFormat = outFmt;
    }

    //Interleaved
    public int Convert(Span<float> src, Span<float> dst)
    {
        CheckBufferSizes(src.Length, dst.Length);
        fixed (float* pIn = src, pOut = dst) {
            return Convert(pIn, src.Length / InputFormat.NumChannels,
                            pOut, dst.Length / OutputFormat.NumChannels);
        }
    }
    public int Convert(Span<short> src, Span<short> dst)
    {
        CheckBufferSizes(src.Length, dst.Length);
        fixed (short* pIn = src, pOut = dst) {
            return Convert(pIn, src.Length / InputFormat.NumChannels,
                            pOut, dst.Length / OutputFormat.NumChannels);
        }
    }

    /// <summary> Resamples the src samples into the dst buffer. (Interleaved->Interleaved) </summary>
    /// <param name="srcCount">Number of samples (per channel) in the src buffer.</param>
    /// <param name="dstCount">Capacity, in samples (per channel) of the dst buffer.</param>
    /// <returns>The number of samples written to the dst buffer.</returns>
    public int Convert(float* src, int srcCount, float* dst, int dstCount)
    {
        return Convert((byte*)src, srcCount, (byte*)dst, dstCount);
    }

    /// <summary> Resamples the src samples into the dst buffer. (Interleaved->Interleaved) </summary>
    /// <param name="srcCount">Number of samples in the src buffer.</param>
    /// <param name="dstCount">Capacity, in samples of the dst buffer.</param>
    /// <returns>The number of samples written to the dst buffer.</returns>
    public int Convert(short* src, int srcCount, short* dst, int dstCount)
    {
        return Convert((byte*)src, srcCount, (byte*)dst, dstCount);
    }

    /// <summary> Resamples the src samples into the dst buffer. (Planar->Interleaved) </summary>
    /// <param name="srcCount">Number of samples (per channel) in the src buffer.</param>
    /// <param name="dstCount">Capacity, in samples (per channel) of the dst buffer.</param>
    /// <returns>The number of samples written to the dst buffer.</returns>
    public int Convert(byte** src, int srcCount, byte* dst, int dstCount)
    {
        byte** p = stackalloc byte*[1] { dst };

        return Convert(src, srcCount,
                        p, dstCount);
    }

    /// <summary> Resamples the src samples into the dst buffer. (Interleaved->Planar) </summary>
    /// <param name="srcCount">Number of samples (per channel) in the src buffer.</param>
    /// <param name="dstCount">Capacity, in samples (per channel) of the dst buffer.</param>
    /// <returns>The number of samples written to the dst buffer.</returns>
    public int Convert(byte* src, int srcCount, byte** dst, int dstCount)
    {
        byte** p = stackalloc byte*[1] { src };

        return Convert(p, srcCount,
                        dst, dstCount);
    }

    /// <summary> Resamples the src samples into the dst buffer. (Interleaved->Interleaved) </summary>
    /// <param name="srcCount">Number of samples (per channel) in the src buffer.</param>
    /// <param name="dstCount">Capacity, in samples (per channel) of the dst buffer.</param>
    /// <returns>The number of samples written to the dst buffer.</returns>
    public int Convert(byte* src, int srcCount, byte* dst, int dstCount)
    {
        byte** p = stackalloc byte*[2] { src, dst };

        return Convert(p + 0, srcCount,
                        p + 1, dstCount);
    }

    public int Convert(byte** src, int srcCount, byte** dst, int dstCount)
    {
        return ffmpeg.swr_convert(Context, dst, dstCount, src, srcCount).CheckError();
    }
    public int Convert(AudioFrame src, AudioFrame dst)
    {
        return ffmpeg.swr_convert(Context, dst.Data, dst.Count, src.Data, src.Count).CheckError();
    }
    public int Convert(AVFrame* src, AVFrame* dst)
    {
        return ffmpeg.swr_convert_frame(Context, dst, src).CheckError();
    }

    private void CheckBufferSizes(int srcLen, int dstLen)
    {
        if (srcLen % InputFormat.NumChannels != 0 || dstLen % OutputFormat.NumChannels != 0) {
            throw new ArgumentException("Buffer sizes must be aligned to channel count");
        }
    }

    protected override void Free()
    {
        if (_ctx != null) {
            fixed (SwrContext** s = &_ctx) ffmpeg.swr_free(s);
        }
    }
    private void ThrowIfDisposed()
    {
        if (_ctx == null) {
            throw new ObjectDisposedException(nameof(SwResampler));
        }
    }
}