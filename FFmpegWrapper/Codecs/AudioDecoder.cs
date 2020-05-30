using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public unsafe class AudioDecoder : MediaDecoder
    {
        public AudioFormat AudioFormat
        {
            get => new AudioFormat(Context);
            set {
                Context->sample_rate = value.SampleRate;
                Context->channels = value.Channels;
                Context->sample_fmt = value.SampleFormat;
                Context->channel_layout = value.ChannelLayout;
            }
        }
        public int SampleRate
        {
            get => Context->sample_rate;
            set => SetOrThrowIfOpen(ref Context->sample_rate, value);
        }
        public int Channels
        {
            get => Context->channels;
            set => SetOrThrowIfOpen(ref Context->channels, value);
        }
        public AVSampleFormat SampleFormat
        {
            get => Context->sample_fmt;
            set => SetOrThrowIfOpen(ref Context->sample_fmt, value);
        }
        public ulong ChannelLayout
        {
            get => Context->channel_layout;
            set => SetOrThrowIfOpen(ref Context->channel_layout, value);
        }

        public int FrameSize => Context->frame_size;

        public AudioDecoder(AVCodecID codec) : base(codec, AVMediaType.AVMEDIA_TYPE_AUDIO)
        {
        }
        public AudioDecoder(AVCodecContext* ctx) : base(ctx, AVMediaType.AVMEDIA_TYPE_AUDIO)
        {
        }

        /// <summary> Allocates a frame that can be used in ReceiveFrame() methods. </summary>
        public AudioFrame AllocateFrame()
        {
            if (SampleFormat == AVSampleFormat.AV_SAMPLE_FMT_NONE) {
                throw new InvalidOperationException("Invalid sample format. (Is the decoder open?)");
            }
            int size = FrameSize;
            if (size <= 0) {
                size = 4096;
            }
            return new AudioFrame(AudioFormat, size);
        }
    }
}
