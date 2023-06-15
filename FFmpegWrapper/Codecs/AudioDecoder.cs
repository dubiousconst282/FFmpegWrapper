namespace FFmpeg.Wrapper;

public unsafe class AudioDecoder : MediaDecoder
{
    public AVSampleFormat SampleFormat => _ctx->sample_fmt;
    public int SampleRate => _ctx->sample_rate;
    public int NumChannels => _ctx->ch_layout.nb_channels;
    public AVChannelLayout ChannelLayout => _ctx->ch_layout;

    public AudioFormat Format => new(_ctx);
    
    public AudioDecoder(AVCodecID codecId)
        : this(MediaCodec.GetDecoder(codecId)) { }

    public AudioDecoder(MediaCodec codec)
        : this(AllocContext(codec), takeOwnership: true) { }

    public AudioDecoder(AVCodecContext* ctx, bool takeOwnership)
        : base(ctx, MediaTypes.Audio, takeOwnership) { }
}