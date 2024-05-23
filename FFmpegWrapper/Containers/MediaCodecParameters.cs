namespace FFmpeg.Wrapper;

/// <inheritdoc cref="AVCodecParameters" />
public unsafe readonly struct MediaCodecParameters
{
    public AVCodecParameters* Handle { get; }

    public MediaCodecParameters(AVCodecParameters* handle) => Handle = handle;

    /// <inheritdoc cref="AVCodecParameters.codec_type" />
    public AVMediaType CodecType => Handle->codec_type;

    /// <inheritdoc cref="AVCodecParameters.codec_id" />
    public AVCodecID CodecId => Handle->codec_id;

    /// <inheritdoc cref="AVCodecParameters.codec_tag" />
    public uint CodecTag => Handle->codec_tag;

    /// <inheritdoc cref="AVCodecParameters.extradata" />
    public ReadOnlySpan<byte> ExtraData => new(Handle->extradata, Handle->extradata_size);

    /// <inheritdoc cref="AVCodecParameters.bit_rate" />
    public long BitRate => Handle->bit_rate;

    /// <inheritdoc cref="AVCodecParameters.bits_per_coded_sample" />
    public int BitsPerCodedSample => Handle->bits_per_coded_sample;

    /// <inheritdoc cref="AVCodecParameters.bits_per_raw_sample" />
    public int BitsPerRawSample => Handle->bits_per_raw_sample;

    /// <inheritdoc cref="AVCodecParameters.profile" />
    public int Profile => Handle->profile;

    /// <summary> Shorthand for <c>ffmpeg.avcodec_profile_name(CodecId, Profile)</c>. </summary>
    public string ProfileName => ffmpeg.avcodec_profile_name(CodecId, Profile);

    /// <inheritdoc cref="AVCodecParameters.level" />
    public int Level => Handle->level;

    //Video fields

    /// <inheritdoc cref="AVCodecParameters.width" />
    public int Width => Handle->width;

    /// <inheritdoc cref="AVCodecParameters.width" />
    public int Height => Handle->height;

    public AVPixelFormat PixelFormat => (AVPixelFormat)Handle->format;

    /// <inheritdoc cref="AVCodecParameters.sample_aspect_ratio" />
    public Rational PixelAspectRatio => Handle->sample_aspect_ratio;

    public PictureFormat PictureFormat => new(Width, Height, PixelFormat, PixelAspectRatio);

    /// <inheritdoc cref="AVCodecParameters.framerate"/>
    public Rational FrameRate => Handle->framerate;

    /// <inheritdoc cref="AVCodecParameters.field_order" />
    public AVFieldOrder FieldOrder => Handle->field_order;

    /// <inheritdoc cref="AVCodecParameters.color_range" />
    public AVColorRange ColorRange => Handle->color_range;

    /// <inheritdoc cref="AVCodecParameters.color_primaries" />
    public AVColorPrimaries ColorPrimaries => Handle->color_primaries;

    /// <inheritdoc cref="AVCodecParameters.color_trc" />
    public AVColorTransferCharacteristic ColorTrc => Handle->color_trc;

    /// <inheritdoc cref="AVCodecParameters.color_space" />
    public AVColorSpace ColorMatrix => Handle->color_space;

    /// <inheritdoc cref="AVCodecParameters.chroma_location" />
    public AVChromaLocation ChromaLocation => Handle->chroma_location;

    public PictureColorspace Colorspace => new(ColorMatrix, ColorPrimaries, ColorTrc, ColorRange);

    /// <inheritdoc cref="AVCodecParameters.video_delay" />
    public int VideoDelay => Handle->video_delay;

    //Audio fields

    /// <inheritdoc cref="AVCodecParameters.sample_rate" />
    public int SampleRate => Handle->sample_rate;

    /// <inheritdoc cref="AVCodecParameters.block_align" />
    public int BlockAlign => Handle->block_align;

    /// <inheritdoc cref="AVCodecParameters.frame_size" />
    public int FrameSize => Handle->frame_size;

    /// <inheritdoc cref="AVCodecParameters.initial_padding" />
    public int InitialPaddingSamples => Handle->initial_padding;

    /// <inheritdoc cref="AVCodecParameters.trailing_padding" />
    public int TrailingPaddingSamples => Handle->trailing_padding;

    /// <inheritdoc cref="AVCodecParameters.seek_preroll" />
    public int SeekPrerollSamples => Handle->seek_preroll;

    /// <inheritdoc cref="AVCodecParameters.ch_layout" />
    public ChannelLayout ChannelLayout => ChannelLayout.FromExisting(&Handle->ch_layout);

    public int NumChannels => Handle->ch_layout.nb_channels;
    public AVSampleFormat SampleFormat => (AVSampleFormat)Handle->format;

    public AudioFormat AudioFormat => new(SampleFormat, SampleRate, ChannelLayout);

    /// <inheritdoc cref="AVCodecParameters.coded_side_data"/>
    public PacketSideDataList CodedSideData => new(&Handle->coded_side_data, &Handle->nb_coded_side_data);
}