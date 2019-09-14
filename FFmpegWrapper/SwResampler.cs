using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public unsafe class SwResampler : IDisposable
    {
        private SwrContext* _ctx;

        public SwrContext* Context
        {
            get {
                ThrowIfDisposed();
                return _ctx;
            }
        }

        public AudioFormat SourceFormat { get; }
        public AudioFormat DestinationFormat { get; }

        /// <summary>
        /// Gets the number of dst buffered samples
        /// </summary>
        public int BufferedSamples => (int)ffmpeg.swr_get_delay(_ctx, DestinationFormat.SampleRate);

        public SwResampler(AudioFormat src, AudioFormat dst)
        {
            _ctx = ffmpeg.swr_alloc();
            ffmpeg.av_opt_set_int(_ctx, "in_channel_layout", (long)src.ChannelLayout, 0);
            ffmpeg.av_opt_set_int(_ctx, "in_channel_count", src.Channels, 0);
            ffmpeg.av_opt_set_int(_ctx, "in_sample_rate", src.SampleRate, 0);
            ffmpeg.av_opt_set_int(_ctx, "in_sample_fmt", (long)src.SampleFormat, 0);

            ffmpeg.av_opt_set_int(_ctx, "out_channel_layout", (long)dst.ChannelLayout, 0);
            ffmpeg.av_opt_set_int(_ctx, "out_channel_count", dst.Channels, 0);
            ffmpeg.av_opt_set_int(_ctx, "out_sample_rate", dst.SampleRate, 0);
            ffmpeg.av_opt_set_int(_ctx, "out_sample_fmt", (long)dst.SampleFormat, 0);

            ffmpeg.swr_init(_ctx);

            SourceFormat = src;
            DestinationFormat = dst;
        }

        //Interleaved
        public int Resample(Span<float> src, Span<float> dst)
        {
            CheckBufferSizes(src.Length, dst.Length);
            fixed (float* pIn = src, pOut = dst) {
                return Resample(pIn, src.Length / SourceFormat.Channels,
                                pOut, dst.Length / DestinationFormat.Channels);
            }
        }

        public int Resample(float* src, int srcCount, float* dst, int dstCount)
        {
            return Resample((byte*)src, srcCount, (byte*)dst, dstCount);
        }

        public int Resample(Span<short> src, Span<short> dst)
        {
            CheckBufferSizes(src.Length, dst.Length);
            fixed (short* pIn = src, pOut = dst) {
                return Resample(pIn, src.Length / SourceFormat.Channels,
                                pOut, dst.Length / DestinationFormat.Channels);
            }
        }

        /// <summary> Resamples the src samples into the dst buffer. (Interleaved->Interleaved) </summary>
        /// <param name="srcCount">Number of samples contained by the src parameter.</param>
        /// <param name="dstCount">Number of samples contained by the dst parameter.</param>
        /// <returns>The number of samples contained in the dst buffer.</returns>
        public int Resample(short* src, int srcCount, short* dst, int dstCount)
        {
            return Resample((byte*)src, srcCount, (byte*)dst, dstCount);
        }

        /// <summary> Resamples the src samples into the dst buffer. (Planar->Interleaved) </summary>
        /// <param name="srcCount">Number of samples contained by the src parameter.</param>
        /// <param name="dstCount">Number of samples contained by the dst parameter.</param>
        /// <returns>The number of samples contained in the dst buffer.</returns>
        public int Resample(byte** src, int srcCount, byte* dst, int dstCount)
        {
            byte** p = stackalloc byte*[1] { dst };

            return Resample(src, srcCount,
                            p, dstCount);
        }

        /// <summary> Resamples the src samples into the dst buffer. (Interleaved->Planar) </summary>
        /// <param name="srcCount">Number of samples contained by the src parameter.</param>
        /// <param name="dstCount">Number of samples contained by the dst parameter.</param>
        /// <returns>The number of samples contained in the dst buffer.</returns>
        public int Resample(byte* src, int srcCount, byte** dst, int dstCount)
        {
            byte** p = stackalloc byte*[1] { src };

            return Resample(p, srcCount,
                            dst, dstCount);
        }

        /// <summary> Resamples the src samples into the dst buffer. (Interleaved->Interleaved) </summary>
        /// <param name="srcCount">Number of samples contained by the src parameter.</param>
        /// <param name="dstCount">Number of samples contained by the dst parameter.</param>
        /// <returns>The number of samples contained in the dst buffer.</returns>
        public int Resample(byte* src, int srcCount, byte* dst, int dstCount)
        {
            byte** p = stackalloc byte*[2] { src, dst };

            return Resample(p + 0, srcCount,
                            p + 1, dstCount);
        }

        public int Resample(AudioFrame src, AudioFrame dst)
        {
            return ffmpeg.swr_convert(Context, dst.Planes, dst.Capacity, src.Planes, src.Count).CheckError();
        }

        //Call to swr_convert
        public int Resample(byte** src, int srcCount, byte** dst, int dstCount)
        {
            return ffmpeg.swr_convert(Context, dst, dstCount, src, srcCount).CheckError();
        }

        public int Resample(AVFrame* src, AVFrame* dst)
        {
            return ffmpeg.swr_convert_frame(Context, dst, src).CheckError();
        }

        private void ThrowIfDisposed()
        {
            if (_ctx == null) {
                throw new ObjectDisposedException(nameof(SwResampler));
            }
        }

        private void CheckBufferSizes(int srcLen, int dstLen)
        {
            if (srcLen % SourceFormat.Channels != 0) {
                throw new ArgumentException("Buffer size must be a multiple of SourceFormat.Channels.", "src");
            }
            if (dstLen % DestinationFormat.Channels != 0) {
                throw new ArgumentException("Buffer size must be a multiple of DestinationFormat.Channels.", "dst");
            }
        }

        public void Dispose()
        {
            if (_ctx != null) {
                fixed (SwrContext** s = &_ctx) ffmpeg.swr_free(s);
            }
        }
    }
}
