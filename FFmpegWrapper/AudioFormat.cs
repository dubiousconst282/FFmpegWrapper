using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public struct AudioFormat
    {
        public AVSampleFormat SampleFormat { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public ulong ChannelLayout { get; }

        public int BytesPerSample => ffmpeg.av_get_bytes_per_sample(SampleFormat);
        public bool IsPlanar => ffmpeg.av_sample_fmt_is_planar(SampleFormat) != 0;

        public AudioFormat(AVSampleFormat fmt, int rate, int ch)
        {
            SampleFormat = fmt;
            SampleRate = rate;
            Channels = ch;
            ChannelLayout = (ulong)ffmpeg.av_get_default_channel_layout(Channels);
        }
        public AudioFormat(AVSampleFormat fmt, int rate, int ch, ulong layout)
        {
            SampleFormat = fmt;
            SampleRate = rate;
            Channels = ch;
            ChannelLayout = layout;
        }
        public unsafe AudioFormat(AVCodecContext* ctx)
        {
            if (ctx->codec_type != AVMediaType.AVMEDIA_TYPE_AUDIO) {
                throw new ArgumentException("Codec context media type is not audio.", nameof(ctx));
            }
            SampleFormat = ctx->sample_fmt;
            SampleRate = ctx->sample_rate;
            Channels = ctx->channels;
            ChannelLayout = ctx->channel_layout;
        }
        public unsafe AudioFormat(AVFrame* frame)
        {
            if (frame->channels <= 0 || frame->sample_rate <= 0) {
                throw new ArgumentException("The frame does not specify a valid audio format.", nameof(frame));
            }
            SampleFormat = (AVSampleFormat)frame->format;
            SampleRate = frame->sample_rate;
            Channels = frame->channels;
            ChannelLayout = frame->channel_layout;
        }

        public override string ToString()
        {
            var fmt = SampleFormat.ToString().Substring("AV_SAMPLE_FMT_".Length);
            return $"Rate={SampleRate / 1000.0:0.0}KHz Format={fmt} Channels={Channels}";
        }
    }
}
