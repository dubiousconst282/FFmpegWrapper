using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public unsafe class AudioEncoder : MediaEncoder
    {
        public AudioFormat AudioFormat => new AudioFormat(Context);

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

        public AVSampleFormat[] SupportedSampleFormats => FFmpegHelpers.ToArray(Codec->sample_fmts, AVSampleFormat.AV_SAMPLE_FMT_NONE);
        public int[] SupportedSampleRates => FFmpegHelpers.ToArray(Codec->supported_samplerates, 0);

        public AudioEncoder(AVCodecID codec)
            : base(codec, AVMediaType.AVMEDIA_TYPE_AUDIO)
        {
        }

        public AudioEncoder(AVCodecID codec, AudioFormat info, int bitrate)
            : this(codec, info.SampleRate, info.Channels, info.SampleFormat, bitrate)
        {
            ChannelLayout = info.ChannelLayout;
        }

        public AudioEncoder(AVCodecID codec, int rate, int channels, AVSampleFormat fmt, int bitrate)
            : base(codec, AVMediaType.AVMEDIA_TYPE_AUDIO)
        {
            SampleRate = rate;
            Channels = channels;
            ChannelLayout = (ulong)ffmpeg.av_get_default_channel_layout(channels);
            SampleFormat = fmt;
            BitRate = bitrate;
            TimeBase = new AVRational() { den = rate, num = 1 };
        }

        public LavResult SendFrame(AudioFrame frame, long timestamp)
        {
            if (frame != null) {
                AVFrame* avf = frame.Frame;
                avf->pts = timestamp;
                return SendFrame(avf);
            } else {
                return SendFrame(null);
            }
        }
    }
}
