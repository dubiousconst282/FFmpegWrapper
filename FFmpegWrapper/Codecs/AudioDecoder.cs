namespace FFmpeg.Wrapper;

public unsafe class AudioDecoder : MediaDecoder
{
    public AVSampleFormat SampleFormat => _ctx->sample_fmt;
    public int SampleRate => _ctx->sample_rate;
    public int NumChannels => _ctx->ch_layout.nb_channels;
    public ChannelLayout ChannelLayout => ChannelLayout.FromExisting(&_ctx->ch_layout);

    public AudioFormat Format => new(SampleFormat, SampleRate, ChannelLayout);

    public AudioDecoder(AVCodecID codecId)
        : this(MediaCodec.GetDecoder(codecId)) { }

    public AudioDecoder(MediaCodec codec)
        : this(AllocContext(codec), takeOwnership: true) { }

    public AudioDecoder(AVCodecContext* ctx, bool takeOwnership)
        : base(ctx, MediaTypes.Audio, takeOwnership) { }
}