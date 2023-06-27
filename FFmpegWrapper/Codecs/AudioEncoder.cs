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
    public ChannelLayout ChannelLayout {
        get => ChannelLayout.FromExisting(&_ctx->ch_layout);
        set {
            ThrowIfOpen();
            value.CopyTo(&_ctx->ch_layout);
        }
    }

    public AudioFormat Format {
        get => new(SampleFormat, SampleRate, ChannelLayout);
        set {
            ThrowIfOpen();
            _ctx->sample_rate = value.SampleRate;
            _ctx->sample_fmt = value.SampleFormat;
            value.Layout.CopyTo(&_ctx->ch_layout);
        }
    }

    /// <summary> Number of samples per channel in an audio frame (set after the encoder is opened). </summary>
    /// <remarks>
    /// Each submitted frame except the last must contain exactly this amount of samples per channel.
    /// May be null when the codec has <see cref="MediaCodecCaps.VariableFrameSize"/> set, then the frame size is not restricted.
    /// </remarks>
    public int? FrameSize => _ctx->frame_size == 0 ? null : _ctx->frame_size;

    public AudioEncoder(AVCodecID codecId, in AudioFormat format, int bitrate = 0)
        : this(MediaCodec.GetEncoder(codecId), format, bitrate) { }

    public AudioEncoder(MediaCodec codec, in AudioFormat format, int bitrate = 0)
        : this(AllocContext(codec), takeOwnership: true)
    {
        Format = format;
        BitRate = bitrate;
        TimeBase = new Rational(1, format.SampleRate);
    }

    public AudioEncoder(AVCodecContext* ctx, bool takeOwnership)
        : base(ctx, MediaTypes.Audio, takeOwnership) { }
}