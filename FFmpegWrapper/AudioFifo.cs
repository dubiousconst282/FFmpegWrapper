using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public unsafe class AudioFifo : IDisposable
    {
        private AVAudioFifo* _fifo; //be careful when using directly.

        public AVAudioFifo* Fifo
        {
            get {
                ThrowIfDisposed();
                return _fifo;
            }
        }

        public AVSampleFormat Format { get; }
        public int Channels { get; }

        public int Size => ffmpeg.av_audio_fifo_size(Fifo);
        public int Space => ffmpeg.av_audio_fifo_space(Fifo);
        public int Capacity => Space + Size;

        public AudioFifo(AudioFormat fmt, int initialSize) : this(fmt.SampleFormat, fmt.Channels, initialSize)
        {
        }

        public AudioFifo(AVSampleFormat fmt, int channels, int initialSize)
        {
            Format = fmt;
            Channels = channels;

            _fifo = ffmpeg.av_audio_fifo_alloc(fmt, channels, initialSize);
            if (_fifo == null) {
                throw new OutOfMemoryException("Could not allocate the audio FIFO.");
            }
        }

        public void Write(AudioFrame frame)
        {
            if (frame.SampleFormat != Format || frame.Channels != Channels) {
                throw new ArgumentException("Incompatible frame format.", nameof(frame));
            }
            Write(frame.Planes, frame.Count);
        }
        public void Write(Span<short> src)
        {
            if (Format != AVSampleFormat.AV_SAMPLE_FMT_S16) {
                throw new InvalidOperationException("Incompatible format.");
            }
            fixed (short* s = src) {
                Write((byte*)s, src.Length / Channels);
            }
        }
        public void Write(Span<float> src)
        {
            if (Format != AVSampleFormat.AV_SAMPLE_FMT_FLT) {
                throw new InvalidOperationException("Incompatible format.");
            }
            fixed (float* s = src) {
                Write((byte*)s, src.Length / Channels);
            }
        }
        public void Write(byte* samples, int count)
        {
            byte** planes = stackalloc byte*[1] { samples };
            Write(planes, count);
        }
        public void Write(byte** planes, int count)
        {
            ffmpeg.av_audio_fifo_write(Fifo, (void**)planes, count);
        }

        public int Read(AudioFrame frame, int count)
        {
            if (count <= 0) {
                throw new ArgumentOutOfRangeException(nameof(count), "Must be at least 1.");
            }

            if (count > frame.Capacity) {
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot exceed the capacity size.");
            }
            if (frame.SampleFormat != Format || frame.Channels != Channels) {
                throw new ArgumentException("Incompatible frame format.", nameof(frame));
            }

            return frame.Count = Read(frame.Planes, count);
        }
        public int Read(Span<short> dest)
        {
            if (Format != AVSampleFormat.AV_SAMPLE_FMT_S16) {
                throw new InvalidOperationException("Incompatible format.");
            }
            fixed (short* s = dest) {
                return Read((byte*)s, dest.Length / Channels);
            }
        }
        public int Read(Span<float> dest)
        {
            if (Format != AVSampleFormat.AV_SAMPLE_FMT_FLT) {
                throw new InvalidOperationException("Incompatible format.");
            }
            fixed (float* s = dest) {
                return Read((byte*)s, dest.Length / Channels);
            }
        }
        public int Read(byte* dest, int count)
        {
            byte** planes = stackalloc byte*[1] { dest };
            return Read(planes, count);
        }
        public int Read(byte** dest, int count)
        {
            return ffmpeg.av_audio_fifo_read(Fifo, (void**)dest, count);
        }

        public void Clear()
        {
            ffmpeg.av_audio_fifo_reset(Fifo);
        }

        private void ThrowIfDisposed()
        {
            if (_fifo == null) {
                throw new ObjectDisposedException(nameof(AudioFifo));
            }
        }

        public void Dispose()
        {
            if (_fifo != null) {
                ffmpeg.av_audio_fifo_free(_fifo);
                _fifo = null;
            }
        }
    }
}