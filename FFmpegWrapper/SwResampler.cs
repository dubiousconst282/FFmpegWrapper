namespace FFmpeg.Wrapper;

public unsafe class SwResampler : FFObject
{
    private SwrContext* _ctx;

    public SwrContext* Handle {
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

    /// <summary> Converts interleaved audio samples from <paramref name="src"/> to <paramref name="dst"/>. </summary>
    /// <remarks> <paramref name="src"/> can be set to <see langword="default"/> to flush the last few samples out at the end. </remarks>
    /// <returns> The number of samples written to the dst buffer. </returns>
    public int Convert<TSrc, TDst>(ReadOnlySpan<TSrc> src, Span<TDst> dst)
        where TSrc : unmanaged 
        where TDst : unmanaged
    {
        if (InputFormat.IsPlanar || OutputFormat.IsPlanar) {
            throw new InvalidOperationException("This overload does not support planar formats.");
        }
        if (src.Length % InputFormat.NumChannels != 0 || dst.Length % OutputFormat.NumChannels != 0) {
            throw new ArgumentException("Buffer sizes must be aligned to channel count.");
        }
        if (InputFormat.BytesPerSample != sizeof(TSrc) || OutputFormat.BytesPerSample != sizeof(TDst)) {
            throw new ArgumentException("Buffer types must match resampler format.");
        }

        fixed (TSrc* pSrc = src)
        fixed (TDst* pDst = dst) {
            var ppSrc = pSrc == null ? null : &pSrc;
            return Convert((byte**)ppSrc, src.Length / InputFormat.NumChannels,
                           (byte**)&pDst, dst.Length / OutputFormat.NumChannels);
        }
    }

    /// <summary> Converts audio data from <paramref name="src"/> to <paramref name="dst"/>. </summary>
    /// <remarks> 
    /// <paramref name="src"/> can be set to <see langword="null"/> to flush the last few samples out at the end. <br/>
    /// If more input is provided than output space, then the input will be buffered.
    /// You can avoid this buffering by using <see cref="GetOutputSamples(int)"/> to retrieve an
    /// upper bound on the required number of output samples for the given number of
    /// input samples. Conversion will run directly without copying whenever possible.
    /// </remarks>
    /// <param name="srcCount">Number of samples (per channel) in the src buffer.</param>
    /// <param name="dstCount">Capacity, in samples (per channel) of the dst buffer.</param>
    /// <returns>The number of samples written to the dst buffer.</returns>
    public int Convert(byte** src, int srcCount, byte** dst, int dstCount)
    {
        return ffmpeg.swr_convert(Handle, dst, dstCount, src, srcCount).CheckError();
    }

    public int Convert(AudioFrame? src, AudioFrame dst)
    {
        return ffmpeg.swr_convert_frame(Handle, dst.Handle, src == null ? null : src.Handle).CheckError();
    }

    private AudioFrame? _currentDest; //dest frame for stateful buffered BeginConvert()/Drain(). 
    private bool _flushing;

    /// <summary>
    /// Converts audio frames from <paramref name="source"/> into an internal buffer, and binds <paramref name="dest"/> as the destination buffer.
    /// <br/>
    /// Converted frames are acquired by calling <see cref="Drain()"/> in a loop, where each successful call indicates that <paramref name="dest"/> 
    /// is filled with exactly <see cref="AudioFrame.Capacity"/> samples. Afterwards it will return false and dest will only be partially filled.
    /// <br/>
    /// Setting <paramref name="source"/> to null will begin flushing, in which case Drain() may return true with partially filled frames on the last time.
    /// </summary>
    /// <exception cref="InvalidOperationException">If there's an activ</exception>
    public void BeginConvert(AudioFrame? source, AudioFrame dest)
    {
        if (_currentDest != null) {
            throw new InvalidOperationException("Cannot call BeginConvert() again until the current buffer is fully drained.");
        }
        _currentDest = dest;
        byte** inputData = null;
        int inputLen = 0;

        if (source != null) {
            inputData = source.Data;
            inputLen = source.Count;
        } else {
            _flushing = true;
        }
        Convert(inputData, inputLen, null, 0);
        //TODO: prevent a copy by converting the first few samples directly to `dest`
    }

    /// <summary> Drains converted samples to the current dest frame. See <see cref="BeginConvert(AudioFrame?, AudioFrame)"/>. </summary>
    public bool Drain()
    {
        var dest = _currentDest ?? throw new InvalidOperationException($"No bound destination frame. Try calling {nameof(BeginConvert)}() first.");

        if (dest.Count >= dest.Capacity) {
            dest.Count = 0;
        }
        //Convert buffered frames to the current dest, starting at the frame count
        int remainingCount = dest.Capacity - dest.Count;
        int outOffset = dest.Count * dest.Format.BytesPerSample * (dest.IsPlanar ? 1 : dest.NumChannels);
        int numPlanes = dest.IsPlanar ? dest.NumChannels : 1;
        byte** outData = stackalloc byte*[numPlanes];

        for (int i = 0; i < numPlanes; i++) {
            outData[i] = &dest.Data[i][outOffset];
        }
        var inData = _flushing ? null : outData; //input must be non-null to prevent a flush.
        int actualOut = Convert(inData, 0, outData, remainingCount);

        dest.Count += actualOut;

        if (dest.Count != dest.Capacity) {
            //No more buffered data, data must be feed via BeginConvert() again.
            _currentDest = null;
            _flushing = false;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Find an upper bound on the number of samples that the next convert call will output, if 
    /// called with <paramref name="inputSampleCount"/> of input samples. 
    /// 
    /// This depends on the internal state, and anything changing the internal state 
    /// (like further convert() calls) may change the number of samples this method returns
    /// for the same number of input samples. 
    /// </summary>
    public int GetOutputSamples(int inputSampleCount)
    {
        return ffmpeg.swr_get_out_samples(_ctx, inputSampleCount);
    }

    protected override void Free()
    {
        if (_ctx != null) {
            fixed (SwrContext** s = &_ctx) {
                ffmpeg.swr_free(s);
            }
        }
    }
    private void ThrowIfDisposed()
    {
        if (_ctx == null) {
            throw new ObjectDisposedException(nameof(SwResampler));
        }
    }
}