namespace FFmpeg.Wrapper;

public unsafe class AudioQueue : FFObject
{
    private AVAudioFifo* _fifo;

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
    public void Write<T>(Span<T> src) where T : unmanaged
    {
        CheckFormatForInterleavedBuffer(src.Length, sizeof(T));

        fixed (T* pSrc = src) {
            Write((byte**)&pSrc, src.Length / NumChannels);
        }
    }
    public void Write(byte** channels, int count)
    {
        ffmpeg.av_audio_fifo_write(Handle, (void**)channels, count);
    }

    public int Read(AudioFrame frame, int count)
    {
        if (count <= 0 || count > frame.Capacity) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        if (frame.SampleFormat != Format || frame.NumChannels != NumChannels) {
            throw new InvalidOperationException("Incompatible frame format.");
        }
        return frame.Count = Read(frame.Data, count);
    }

    public int Read<T>(Span<T> dest) where T : unmanaged
    {
        CheckFormatForInterleavedBuffer(dest.Length, sizeof(T));

        fixed (T* pDest = dest) {
            return Read((byte**)&pDest, dest.Length / NumChannels);
        }
    }
    public int Read(byte** dest, int count)
    {
        return ffmpeg.av_audio_fifo_read(Handle, (void**)dest, count);
    }

    public void Clear()
    {
        ffmpeg.av_audio_fifo_reset(Handle);
    }
    public void Drain(int count)
    {
        ffmpeg.av_audio_fifo_drain(Handle, count).CheckError();
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

    private void CheckFormatForInterleavedBuffer(int length, int sampleSize)
    {
        if (ffmpeg.av_get_bytes_per_sample(Format) != sampleSize ||
            ffmpeg.av_sample_fmt_is_planar(Format) != 0 ||
            length % NumChannels != 0
        ) {
            throw new InvalidOperationException("Incompatible buffer format.");
        }
    }
}