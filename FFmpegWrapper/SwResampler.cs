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

    /// <summary> Gets an estimated number of buffered output samples. </summary>
    public int BufferedSamples => (int)ffmpeg.swr_get_delay(_ctx, OutputFormat.SampleRate);

    public SwResampler(AudioFormat inFmt, AudioFormat outFmt)
    {
        _ctx = ffmpeg.swr_alloc();

        var tempLayout = inFmt.Layout.Native;
        ffmpeg.av_opt_set_chlayout(_ctx, "in_chlayout", &tempLayout, 0);
        ffmpeg.av_opt_set_int(_ctx, "in_sample_rate", inFmt.SampleRate, 0);
        ffmpeg.av_opt_set_int(_ctx, "in_sample_fmt", (long)inFmt.SampleFormat, 0);

        tempLayout = outFmt.Layout.Native;
        ffmpeg.av_opt_set_chlayout(_ctx, "out_chlayout", &tempLayout, 0);
        ffmpeg.av_opt_set_int(_ctx, "out_sample_rate", outFmt.SampleRate, 0);
        ffmpeg.av_opt_set_int(_ctx, "out_sample_fmt", (long)outFmt.SampleFormat, 0);

        ffmpeg.swr_init(_ctx);

        InputFormat = inFmt;
        OutputFormat = outFmt;
    }

    /// <summary> Convert interleaved audio samples from <paramref name="src"/> and writes the result to <paramref name="dst"/>. </summary>
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

    /// <summary> Convert audio data from <paramref name="src"/> and writes the result to <paramref name="dst"/>. </summary>
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

    private bool _flushing;

    /// <summary>
    /// Converts audio samples from <paramref name="frame"/> to the internal resampler buffer. The converted samples can be retrieved using <see cref="ReceiveFrame(AudioFrame)"/>.
    /// <br/>
    /// Setting <paramref name="frame"/> to null will transition the resampler state to begin flushing, which may cause ReceiveFrame() to return true with partially filled frames on the last time.
    /// </summary>
    public void SendFrame(AudioFrame? frame)
    {
        byte** inputData = null;
        int inputLen = 0;

        if (frame != null) {
            inputData = frame.Data;
            inputLen = frame.Count;
        } else {
            _flushing = true;
        }
        Convert(inputData, inputLen, null, 0);
    }

    /// <summary>
    /// Retrieves converted samples from the internal buffer and appends them to <paramref name="frame"/>. <br/>
    /// If the frame is already full when this method is called, it will be reset before being filled.
    /// </summary>
    /// <returns>
    /// True if the frame was filled up to its <see cref="AudioFrame.Capacity"/>, or if the resampler 
    /// has finished flushing out the final samples.  <br/>
    /// False if the frame was only partially filled and more input data must be feed via <see cref="SendFrame(AudioFrame?)"/>.
    /// </returns>
    public bool ReceiveFrame(AudioFrame frame)
    {
        if (frame.Count >= frame.Capacity) {
            frame.Count = 0;
        }
        //Calculate remaining space and starting pointers
        int count = frame.Capacity - frame.Count;
        int offset = frame.Count * frame.Format.BytesPerSample * (frame.IsPlanar ? 1 : frame.NumChannels);
        int numPlanes = frame.IsPlanar ? frame.NumChannels : 1;
        byte** data = stackalloc byte*[numPlanes];

        for (int i = 0; i < numPlanes; i++) {
            data[i] = &frame.Data[i][offset];
        }
        var inData = _flushing ? null : data; //input ptr must be non-null to prevent transitioning the resampler to flush state.
        int actualOut = Convert(inData, 0, data, count);

        frame.Count += actualOut;
        return frame.Count >= frame.Capacity;
    }

    /// <summary> Retrieves converted samples from the internal buffer. </summary>
    /// <returns> The number of samples written to <paramref name="buffer"/>. </returns>
    public int ReceiveFrame<T>(Span<T> buffer) where T : unmanaged
    {
        if (OutputFormat.IsPlanar) {
            throw new InvalidOperationException("This overload does not support planar output formats.");
        }
        fixed (T* pBuffer = buffer) {
            byte** outData = stackalloc byte*[1] { (byte*)pBuffer };
            int count = buffer.Length * sizeof(T) / (OutputFormat.NumChannels * OutputFormat.BytesPerSample);

            var inData = _flushing ? null : outData; //input ptr must be non-null to prevent transitioning the resampler to flush state.
            return Convert(inData, 0, outData, count);
        }
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

    /// <summary> Drops the specified number of output samples. </summary>
    public void DropOutputSamples(int count)
    {
        ffmpeg.swr_drop_output(_ctx, count).CheckError();
    }

    //TODO: expose swr_next_pts() and whatever else

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