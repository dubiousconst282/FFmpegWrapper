namespace FFmpeg.Wrapper;

public unsafe class AudioFrame : MediaFrame
{
    public AVSampleFormat SampleFormat => (AVSampleFormat)_frame->format;
    public int SampleRate => _frame->sample_rate;
    public int NumChannels => _frame->ch_layout.nb_channels;

    public AudioFormat Format => new AudioFormat(_frame);

    public byte** Data => (byte**)&_frame->data;
    public int Stride => _frame->linesize[0];

    public bool IsPlanar => ffmpeg.av_sample_fmt_is_planar(SampleFormat) != 0;

    public int Count {
        get => _frame->nb_samples;
        set {
            if (value < 0 || value > Capacity) {
                throw new ArgumentOutOfRangeException(nameof(value), "Must must be positive and not exceed the frame capacity.");
            }
            _frame->nb_samples = value;
        }
    }

    public int Capacity => Stride / (ffmpeg.av_get_bytes_per_sample(SampleFormat) * (IsPlanar ? 1 : NumChannels));

    /// <summary> Allocates a new empty <see cref="AVFrame"/>. </summary>
    public AudioFrame()
        : this(ffmpeg.av_frame_alloc(), takeOwnership: true) { }

    public AudioFrame(AudioFormat fmt, int capacity)
    {
        _frame = ffmpeg.av_frame_alloc();
        _frame->format = (int)fmt.SampleFormat;
        _frame->sample_rate = fmt.SampleRate;
        _frame->ch_layout = fmt.Layout;

        _frame->nb_samples = capacity;
        ffmpeg.av_frame_get_buffer(_frame, 0).CheckError("Failed to allocate frame buffers.");
    }
    public AudioFrame(AVSampleFormat fmt, int sampleRate, int numChannels, int capacity)
        : this(new AudioFormat(fmt, sampleRate, numChannels), capacity) { }

    /// <summary> Wraps an existing <see cref="AVFrame"/> into an <see cref="AudioFrame"/> instance. </summary>
    /// <param name="takeOwnership">True if <paramref name="frame"/> should be freed when Dispose() is called.</param>
    public AudioFrame(AVFrame* frame, bool takeOwnership = false)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
        _ownsFrame = takeOwnership;
    }

    public Span<T> GetChannelSamples<T>(int channel = 0) where T:unmanaged
    {
        if ((uint)channel >= (uint)NumChannels || (!IsPlanar && channel != 0)) {
            throw new ArgumentOutOfRangeException();
        }
        return new Span<T>(Data[channel], Stride / sizeof(T));
    }

    /// <summary> Copy interleaved samples from the span into this frame. </summary>
    /// <returns> Returns the number of samples copied. </returns>
    public int CopyFrom(Span<float> samples) => CopyFrom<float>(samples);

    /// <inheritdoc cref="CopyFrom(Span{float})"/>
    public int CopyFrom(Span<short> samples) => CopyFrom<short>(samples);

    private int CopyFrom<T>(Span<T> samples) where T : unmanaged
    {
        var fmt = Format;
        if (fmt.IsPlanar || fmt.BytesPerSample != sizeof(T)) {
            throw new InvalidOperationException("Incompatible format");
        }
        if (samples.Length % fmt.NumChannels != 0) {
            throw new ArgumentException("Sample count must be a multiple of channel count.", nameof(samples));
        }

        int count = Math.Min(Capacity, samples.Length / fmt.NumChannels);

        fixed (T* ptr = samples) {
            byte** temp = stackalloc byte*[1] { (byte*)ptr };
            ffmpeg.av_samples_copy(_frame->extended_data, temp, 0, 0, count, fmt.NumChannels, fmt.SampleFormat);
        }
        return count;
    }
}