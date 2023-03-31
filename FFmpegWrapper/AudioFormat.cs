namespace FFmpeg.Wrapper;

public unsafe struct AudioFormat
{
    public AVSampleFormat SampleFormat { get; }
    public int SampleRate { get; }
    public AVChannelLayout Layout { get; }

    public int NumChannels => Layout.nb_channels;

    public int BytesPerSample => ffmpeg.av_get_bytes_per_sample(SampleFormat);
    public bool IsPlanar => ffmpeg.av_sample_fmt_is_planar(SampleFormat) != 0;

    public AudioFormat(AVSampleFormat fmt, int sampleRate, int numChannels)
    {
        SampleFormat = fmt;
        SampleRate = sampleRate;

        AVChannelLayout tempLayout;
        ffmpeg.av_channel_layout_default(&tempLayout, numChannels);
        Layout = tempLayout;
    }
    public AudioFormat(AVSampleFormat fmt, int sampleRate, AVChannelLayout layout)
    {
        SampleFormat = fmt;
        SampleRate = sampleRate;
        Layout = layout;
    }
    public AudioFormat(AVCodecContext* ctx)
    {
        if (ctx->codec_type != AVMediaType.AVMEDIA_TYPE_AUDIO) {
            throw new ArgumentException("Codec context media type is not audio.", nameof(ctx));
        }
        SampleFormat = ctx->sample_fmt;
        SampleRate = ctx->sample_rate;
        Layout = ctx->ch_layout;
    }
    public AudioFormat(AVFrame* frame)
    {
        if (frame->ch_layout.nb_channels <= 0 || frame->sample_rate <= 0) {
            throw new ArgumentException("The frame does not specify a valid audio format.", nameof(frame));
        }
        SampleFormat = (AVSampleFormat)frame->format;
        SampleRate = frame->sample_rate;
        Layout = frame->ch_layout;
    }

    public override string ToString()
    {
        var fmt = SampleFormat.ToString().Substring("AV_SAMPLE_FMT_".Length);
        return $"{NumChannels}ch {fmt}, {SampleRate / 1000.0:0.0}KHz";
    }
}