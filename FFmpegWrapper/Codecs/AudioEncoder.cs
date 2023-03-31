namespace FFmpeg.Wrapper;

public unsafe class AudioEncoder : MediaEncoder
{
    public AVSampleFormat SampleFormat {
        get => _ctx->sample_fmt;
        set => SetOrThrowIfOpen(ref _ctx->sample_fmt, value);
    }
    public int SampleRate {
        get => _ctx->sample_rate;
        set => SetOrThrowIfOpen(ref _ctx->sample_rate, value);
    }
    public int NumChannels => _ctx->ch_layout.nb_channels;
    public AVChannelLayout ChannelLayout {
        get => _ctx->ch_layout;
        set => SetOrThrowIfOpen(ref _ctx->ch_layout, value);
    }

    public AudioFormat Format {
        get => new(_ctx);
        set {
            _ctx->sample_rate = value.SampleRate;
            _ctx->sample_fmt = value.SampleFormat;
            _ctx->ch_layout = value.Layout;
        }
    }

    public ReadOnlySpan<AVSampleFormat> SupportedSampleFormats
        => Helpers.GetSpanFromSentinelTerminatedPtr(_ctx->codec->sample_fmts, AVSampleFormat.AV_SAMPLE_FMT_NONE);
    public ReadOnlySpan<int> SupportedSampleRates
        => Helpers.GetSpanFromSentinelTerminatedPtr(_ctx->codec->supported_samplerates, 0);

    public AudioEncoder(AVCodecID codec)
        : base(codec, AVMediaType.AVMEDIA_TYPE_AUDIO) { }
    public AudioEncoder(AVCodecID codec, AudioFormat format, int bitrate)
        : this(codec)
    {
        Format = format;
        BitRate = bitrate;
        TimeBase = new AVRational() { den = format.SampleRate, num = 1 };
    }
}