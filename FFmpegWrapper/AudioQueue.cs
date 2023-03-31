namespace FFmpeg.Wrapper;

public unsafe class AudioQueue : FFObject
{
    private AVAudioFifo* _fifo; //be careful when using directly.

    public AVAudioFifo* Handle {
        get {
            ThrowIfDisposed();
            return _fifo;
        }
    }

    public AVSampleFormat Format { get; }
    public int NumChannels { get; }

    public int Size => ffmpeg.av_audio_fifo_size(_fifo);
    public int Space => ffmpeg.av_audio_fifo_space(_fifo);
    public int Capacity => Space + Size;

    public AudioQueue(AudioFormat fmt, int initialCapacity)
        : this(fmt.SampleFormat, fmt.NumChannels, initialCapacity) { }

    public AudioQueue(AVSampleFormat fmt, int numChannels, int initialCapacity)
    {
        Format = fmt;
        NumChannels = numChannels;

        _fifo = ffmpeg.av_audio_fifo_alloc(fmt, numChannels, initialCapacity);
        if (_fifo == null) {
            throw new OutOfMemoryException("Could not allocate the audio FIFO.");
        }
    }

    public void Write(AudioFrame frame)
    {
        if (frame.SampleFormat != Format || frame.NumChannels != NumChannels) {
            throw new ArgumentException("Incompatible frame format.", nameof(frame));
        }
        Write(frame.Data, frame.Count);
    }
    public void Write(Span<short> src)
    {
        if (Format != AVSampleFormat.AV_SAMPLE_FMT_S16) {
            throw new InvalidOperationException("Incompatible format.");
        }
        fixed (short* s = src) {
            Write((byte*)s, src.Length / NumChannels);
        }
    }
    public void Write(Span<float> src)
    {
        if (Format != AVSampleFormat.AV_SAMPLE_FMT_FLT) {
            throw new InvalidOperationException("Incompatible format.");
        }
        fixed (float* s = src) {
            Write((byte*)s, src.Length / NumChannels);
        }
    }
    public void Write(byte* samples, int count)
    {
        byte** planes = stackalloc byte*[1] { samples };
        Write(planes, count);
    }
    public void Write(byte** planes, int count)
    {
        ffmpeg.av_audio_fifo_write(Handle, (void**)planes, count);
    }

    public int Read(AudioFrame frame, int count)
    {
        if (count <= 0) {
            throw new ArgumentOutOfRangeException(nameof(count), "Must be at least 1.");
        }

        if (count > frame.Capacity) {
            throw new ArgumentOutOfRangeException(nameof(count), "Cannot read more samples than frame capacity.");
        }
        if (frame.SampleFormat != Format || frame.NumChannels != NumChannels) {
            throw new ArgumentException("Incompatible frame format.", nameof(frame));
        }
        return frame.Count = Read(frame.Data, count);
    }
    public int Read(Span<short> dest)
    {
        if (Format != AVSampleFormat.AV_SAMPLE_FMT_S16) {
            throw new InvalidOperationException("Incompatible format.");
        }
        fixed (short* s = dest) {
            return Read((byte*)s, dest.Length / NumChannels);
        }
    }
    public int Read(Span<float> dest)
    {
        if (Format != AVSampleFormat.AV_SAMPLE_FMT_FLT) {
            throw new InvalidOperationException("Incompatible format.");
        }
        fixed (float* s = dest) {
            return Read((byte*)s, dest.Length / NumChannels);
        }
    }
    public int Read(byte* dest, int count)
    {
        byte** planes = stackalloc byte*[1] { dest };
        return Read(planes, count);
    }
    public int Read(byte** dest, int count)
    {
        return ffmpeg.av_audio_fifo_read(Handle, (void**)dest, count);
    }

    public void Clear()
    {
        ffmpeg.av_audio_fifo_reset(Handle);
    }

    protected override void Free()
    {
        if (_fifo != null) {
            ffmpeg.av_audio_fifo_free(_fifo);
            _fifo = null;
        }
    }
    private void ThrowIfDisposed()
    {
        if (_fifo == null) {
            throw new ObjectDisposedException(nameof(AudioQueue));
        }
    }
}