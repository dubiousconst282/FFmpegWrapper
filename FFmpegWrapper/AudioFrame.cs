using System;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public unsafe class AudioFrame : IDisposable
    {
        private AVFrame* _frame;
        public AVFrame* Frame
        {
            get {
                ThrowIfDisposed();
                return _frame;
            }
        }

        public AudioFormat Format => new AudioFormat(Frame);

        public AVSampleFormat SampleFormat => (AVSampleFormat)Frame->format;
        public int SampleRate => Frame->sample_rate;
        public int Channels => Frame->channels;

        public byte** Planes => Frame->extended_data;
        public int Stride => Frame->linesize[0];

        /// <summary> Cached value indicating if the frame format is planar. </summary>
        public bool IsPlanar { get; }

        /// <summary> Actual number of samples per channel contained by this frame.</summary>
        public int Count
        {
            get => Frame->nb_samples;
            set {
                if (value < 0 || value > Capacity) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Must must be positive and not exceed the capacity.");
                }
                Frame->nb_samples = value;
            }
        }

        /// <summary> Maximum number of samples this frame can hold. </summary>
        public int Capacity { get; }

        private readonly bool _ownFrame = true;
        private bool _disposed = false;

        public AudioFrame(AudioFormat fmt, int capacity)
        {
            //https://github.com/FFmpeg/FFmpeg/blob/aceb9131c16918164279cf0f8e1b5384610e3245/libavutil/frame.c#L268

            _frame = ffmpeg.av_frame_alloc();
            _frame->format = (int)fmt.SampleFormat;
            _frame->sample_rate = fmt.SampleRate;
            _frame->channels = fmt.Channels;
            _frame->channel_layout = fmt.ChannelLayout;

            _frame->nb_samples = capacity;
            ffmpeg.av_frame_get_buffer(_frame, 0).CheckError("Failed to allocate frame.");

            _frame->nb_samples = 0;
            Capacity = capacity;
            IsPlanar = fmt.IsPlanar;
        }
        public AudioFrame(int rate, int channels, AVSampleFormat fmt, int capacity)
            : this(new AudioFormat(fmt, rate, channels), capacity)
        {
        }

        /// <summary> 
        /// Wraps an existing <see cref="AVFrame"/> into an <see cref="AudioFrame"/> instance.<br></br>
        /// Note: You should not use this object after the AVFrame is freed.
        /// </summary>
        /// <param name="freeOnDispose">True if the AVFrame should be freed when the Dispose() method is called.</param>
        /// <param name="capacity">Overrides the Capacity property, if greater than 0.</param>
        public AudioFrame(AVFrame* frame, bool freeOnDispose = false, int capacity = -1)
        {
            if (frame->channels <= 0 || frame->sample_rate <= 0 || frame->extended_data == null) {
                throw new ArgumentException("Invalid frame.", nameof(frame));
            }
            _frame = frame;
            Count = frame->nb_samples;
            Capacity = capacity > 0 ? capacity : frame->nb_samples;
            IsPlanar = ffmpeg.av_sample_fmt_is_planar((AVSampleFormat)frame->format) != 0;

            _ownFrame = freeOnDispose;
        }

        /// <summary> Copy the interleaved samples from the span and returns the number of samples copied. </summary>
        public int CopyFrom(Span<float> samples) => CopyFrom<float>(samples);

        /// <summary> Copy the interleaved samples from the span and returns the number of samples copied. </summary>
        public int CopyFrom(Span<short> samples) => CopyFrom<short>(samples);

        private int CopyFrom<T>(Span<T> samples) where T : unmanaged
        {
            var fmt = Format;
            if (fmt.IsPlanar || fmt.BytesPerSample != sizeof(T)) {
                throw new InvalidOperationException("Incompatible format");
            }
            if (samples.Length % fmt.Channels != 0) {
                throw new ArgumentException("Sample count must be a multiple of channel count.", nameof(samples));
            }

            int count = Math.Min(Capacity, samples.Length / fmt.Channels);

            fixed (T* ptr = samples) {
                byte** temp = stackalloc byte*[1] { (byte*)ptr };
                ffmpeg.av_samples_copy(_frame->extended_data, temp, 0, 0, count, fmt.Channels, fmt.SampleFormat);
            }

            return count;
        }

        /// <summary> Copy the samples from <paramref name="frame"/> and returns the number of samples copied. </summary>
        public int CopyFrom(AVFrame* frame)
        {
            ThrowIfDisposed();
            return CopyFrame(frame, _frame, Capacity);
        }

        /// <summary>
        /// Copy the samples to <paramref name="frame"/>.
        /// frame->nb_samples will be set to the number of samples copied.
        /// </summary>
        /// <param name="count">The number of samples to copy. The final value is <code>min(count, this.Count)</code></param>
        public void CopyTo(AVFrame* frame, int count = int.MaxValue)
        {
            ThrowIfDisposed();
            CopyFrame(_frame, frame, Math.Min(_frame->nb_samples, count));
        }

        private int CopyFrame(AVFrame* src, AVFrame* dst, int dstCapacity)
        {
            CheckFormat(src, dst);

            int count = Math.Min(dstCapacity, src->nb_samples);

            ffmpeg.av_samples_copy(dst->extended_data, src->extended_data, 0, 0, count, src->channels, (AVSampleFormat)src->format);
            dst->nb_samples = count;

            return count;
        }

        public float GetSampleFloat(int pos, int ch)
        {
            ValidateGetSetSampleParams(pos, ch, out var fmt);

            switch (fmt) {
                case AVSampleFormat.AV_SAMPLE_FMT_FLT:
                case AVSampleFormat.AV_SAMPLE_FMT_FLTP:
                    return GetSampleUnsafeFloat(pos, ch);

                case AVSampleFormat.AV_SAMPLE_FMT_S16:
                case AVSampleFormat.AV_SAMPLE_FMT_S16P:
                    return GetSampleUnsafeShort(pos, ch) / 32767f;

                default: throw new NotSupportedException();
            }
        }
        public short GetSampleShort(int pos, int ch)
        {
            ValidateGetSetSampleParams(pos, ch, out var fmt);

            switch (fmt) {
                case AVSampleFormat.AV_SAMPLE_FMT_FLT:
                case AVSampleFormat.AV_SAMPLE_FMT_FLTP:
                    return SampleF2S(GetSampleUnsafeFloat(pos, ch));

                case AVSampleFormat.AV_SAMPLE_FMT_S16:
                case AVSampleFormat.AV_SAMPLE_FMT_S16P:
                    return GetSampleUnsafeShort(pos, ch);

                default: throw new NotSupportedException();
            }
        }

        public void SetSampleFloat(int pos, int ch, float s)
        {
            ValidateGetSetSampleParams(pos, ch, out var fmt);

            switch (fmt) {
                case AVSampleFormat.AV_SAMPLE_FMT_FLT:
                case AVSampleFormat.AV_SAMPLE_FMT_FLTP:
                    SetSampleUnsafeFloat(pos, ch, s);
                    break;

                case AVSampleFormat.AV_SAMPLE_FMT_S16:
                case AVSampleFormat.AV_SAMPLE_FMT_S16P:
                    SetSampleUnsafeShort(pos, ch, SampleF2S(s));
                    break;

                default: throw new NotSupportedException();
            }
        }
        public void SetSampleShort(int pos, int ch, short s)
        {
            ValidateGetSetSampleParams(pos, ch, out var fmt);

            switch (fmt) {
                case AVSampleFormat.AV_SAMPLE_FMT_FLT:
                case AVSampleFormat.AV_SAMPLE_FMT_FLTP:
                    SetSampleUnsafeFloat(pos, ch, s / 32767f);
                    break;

                case AVSampleFormat.AV_SAMPLE_FMT_S16:
                case AVSampleFormat.AV_SAMPLE_FMT_S16P:
                    SetSampleUnsafeShort(pos, ch, s);
                    break;

                default: throw new NotSupportedException();
            }
        }

        private void ValidateGetSetSampleParams(int pos, int ch, out AVSampleFormat sampleFmt)
        {
            ThrowIfDisposed();
            var frame = _frame;
            if ((uint)ch >= frame->channels || (uint)pos >= frame->nb_samples) throw new ArgumentOutOfRangeException();

            sampleFmt = (AVSampleFormat)frame->format;
        }

        /// <summary> 
        /// Gets a float sample at the specified position and channel. 
        /// This method does not perform bound checks or sample conversion. 
        /// </summary>
        public float GetSampleUnsafeFloat(int pos, int ch)
        {
            float** p = (float**)_frame->extended_data;

            return IsPlanar ? p[ch][pos]
                            : p[0][pos * Channels + ch];
        }
        /// <summary> 
        /// Gets a 16-bit sample at the specified position and channel. 
        /// This method does not perform bound checks or sample conversion. 
        /// </summary>
        public short GetSampleUnsafeShort(int pos, int ch)
        {
            short** p = (short**)_frame->extended_data;
            return IsPlanar ? p[ch][pos]
                            : p[0][pos * Channels + ch];
        }

        /// <summary> 
        /// Sets a float sample at the specified position and channel. 
        /// This method does not perform bound checks or sample conversion. 
        /// </summary>
        public void SetSampleUnsafeFloat(int pos, int ch, float s)
        {
            float** p = (float**)_frame->extended_data;

            if (IsPlanar) {
                p[ch][pos] = s;
            } else {
                p[0][pos * Channels + ch] = s;
            }
        }

        /// <summary> 
        /// Sets a float sample at the specified position and channel. 
        /// This method does not perform bound checks or sample conversion. 
        /// </summary>
        public void SetSampleUnsafeShort(int pos, int ch, short s)
        {
            short** p = (short**)_frame->extended_data;

            if (IsPlanar) {
                p[ch][pos] = s;
            } else {
                p[0][pos * Channels + ch] = s;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short SampleF2S(float f)
        {
            return (short)((f < -1.0f ? -1.0f : f > 1.0f ? 1.0f : f) * 32767);
        }

        private void CheckFormat(AVFrame* a, AVFrame* b)
        {
            if (a->format != b->format || a->channels != b->channels) {
                throw new ArgumentException("AVFrame must have the same format as this AudioFrame");
            }
        }

        public void Dispose()
        {
            if (!_disposed) {
                if (_ownFrame) { fixed (AVFrame** ppFrame = &_frame) ffmpeg.av_frame_free(ppFrame); }
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(AudioFrame));
            }
        }
    }
}
