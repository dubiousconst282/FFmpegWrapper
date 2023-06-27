namespace FFmpeg.Wrapper;

public unsafe readonly struct AudioFormat : IEquatable<AudioFormat>
{
    public AVSampleFormat SampleFormat { get; }
    public int SampleRate { get; }
    public ChannelLayout Layout { get; }

    public int NumChannels => Layout.NumChannels;
    public int BytesPerSample => ffmpeg.av_get_bytes_per_sample(SampleFormat);
    public bool IsPlanar => ffmpeg.av_sample_fmt_is_planar(SampleFormat) != 0;

    public AudioFormat(AVSampleFormat sampleFmt, int sampleRate, int numChannels)
    {
        SampleFormat = sampleFmt;
        SampleRate = sampleRate;
        Layout = ChannelLayout.GetDefault(numChannels);
    }
    public AudioFormat(AVSampleFormat sampleFmt, int sampleRate, ChannelLayout channelLayout)
    {
        SampleFormat = sampleFmt;
        SampleRate = sampleRate;
        Layout = channelLayout;
    }

    public override string ToString()
    {
        var fmt = SampleFormat.ToString().Substring("AV_SAMPLE_FMT_".Length);
        return $"{SampleRate} Hz, {Layout}, {fmt}";
    }

    public bool Equals(AudioFormat other)
        => other.SampleFormat == SampleFormat && other.SampleRate == SampleRate && other.Layout.Equals(Layout);

    public override bool Equals(object obj) => obj is AudioFormat other && Equals(other);
    public override int GetHashCode() => (SampleRate, NumChannels, (int)SampleFormat).GetHashCode();
}

public unsafe readonly struct ChannelLayout : IEquatable<ChannelLayout>
{
    public readonly AVChannelLayout Native;
    readonly HeapStorage? _heap;

    public ChannelOrder Order => (ChannelOrder)Native.order;
    public int NumChannels => Native.nb_channels;

    /// <inheritdoc cref="ffmpeg.av_channel_layout_channel_from_index(AVChannelLayout*, uint)"/>
    public AudioChannel GetChannel(int index)
    {
        fixed (AVChannelLayout* self = &Native) {
            var ch = (AudioChannel)ffmpeg.av_channel_layout_channel_from_index(self, (uint)index);
            GC.KeepAlive(_heap);
            return ch;
        }
    }

    /// <summary> Get the default channel layout for a given number of channels. </summary>
    public static ChannelLayout GetDefault(int numChannels)
    {
        ChannelLayout layout = default;
        ffmpeg.av_channel_layout_default(&layout.Native, numChannels);
        return layout;
    }

    /// <summary> Initialize a native channel layout from a bitmask indicating which channels are present. </summary>
    /// <exception cref="ArgumentException"></exception>
    public static ChannelLayout FromMask(ulong mask)
    {
        ChannelLayout layout = default;
        if (ffmpeg.av_channel_layout_from_mask(&layout.Native, mask) < 0) {
            throw new ArgumentException();
        }
        return layout;
    }

    /// <summary> Initialize a channel layout from a given string description. </summary>
    /// <remarks>
    /// The input string can be represented by:  <br/>
    ///  - the formal channel layout name (returned by ToString()/av_channel_layout_describe()) <br/>
    ///  - single or multiple channel names (returned by av_channel_name(), eg. "FL",
    ///    or concatenated with "+", each optionally containing a custom name after
    ///    a "@", eg. "FL@Left+FR@Right+LFE") <br/>
    ///  - a decimal or hexadecimal value of a native channel layout (eg. "4" or "0x4")  <br/>
    ///  - the number of channels with default layout (eg. "4c")  <br/>
    ///  - the number of unordered channels (eg. "4C" or "4 channels")  <br/>
    ///  - the ambisonic order followed by optional non-diegetic channels (eg. "ambisonic 2+stereo")  <br/>
    /// </remarks>
    public static ChannelLayout FromString(string str)
    {
        ChannelLayout layout = default;
        if (ffmpeg.av_channel_layout_from_string(&layout.Native, str) < 0) {
            throw new ArgumentException();
        }
        if (layout.Order == ChannelOrder.Custom) {
            Unsafe.AsRef(in layout._heap) = new HeapStorage() { Data = layout.Native.u.map };
        }
        return layout;
    }

    public static ChannelLayout FromExisting(AVChannelLayout* layout)
    {
        ChannelLayout dest = default;

        if (layout->order == AVChannelOrder.AV_CHANNEL_ORDER_CUSTOM) {
            ffmpeg.av_channel_layout_copy(&dest.Native, layout);
            Unsafe.AsRef(in dest._heap) = new HeapStorage() { Data = dest.Native.u.map };
        } else {
            Unsafe.AsRef(in dest.Native) = *layout;
        }
        return dest;
    }

    public void CopyTo(AVChannelLayout* dest)
    {
        fixed (AVChannelLayout* self = &Native) {
            ffmpeg.av_channel_layout_copy(dest, self).CheckError();
            GC.KeepAlive(_heap);
        }
    }

    public override string ToString()
    {
        fixed (AVChannelLayout* self = &Native) {
            var buf = stackalloc byte[1024];
            int ret = ffmpeg.av_channel_layout_describe(self, buf, 1024).CheckError();
            GC.KeepAlive(_heap);
            if (ret > 1024) throw new NotImplementedException();
            return Helpers.PtrToStringUTF8(buf)!;
        }
    }

    public bool Equals(ChannelLayout other)
    {
        fixed (AVChannelLayout* a = &Native) {
            int c = ffmpeg.av_channel_layout_compare(a, &other.Native);
            GC.KeepAlive(_heap);
            GC.KeepAlive(other._heap);
            return c == 0;
        }
    }
    public override bool Equals(object obj) => obj is ChannelLayout other && Equals(other);
    public override int GetHashCode() => NumChannels;

    sealed class HeapStorage
    {
        public AVChannelCustom* Data;
        ~HeapStorage() => ffmpeg.av_free(Data);
    }
}
public enum AudioChannel
{
    None = AVChannel.AV_CHAN_NONE,
    FrontLeft = AVChannel.AV_CHAN_FRONT_LEFT,
    FrontRight = AVChannel.AV_CHAN_FRONT_RIGHT,
    FrontCenter = AVChannel.AV_CHAN_FRONT_CENTER,
    LowFrequency = AVChannel.AV_CHAN_LOW_FREQUENCY,
    BackLeft = AVChannel.AV_CHAN_BACK_LEFT,
    BackRight = AVChannel.AV_CHAN_BACK_RIGHT,
    FrontLeftOfCenter = AVChannel.AV_CHAN_FRONT_LEFT_OF_CENTER,
    FrontRightOfCenter = AVChannel.AV_CHAN_FRONT_RIGHT_OF_CENTER,
    BackCenter = AVChannel.AV_CHAN_BACK_CENTER,
    SideLeft = AVChannel.AV_CHAN_SIDE_LEFT,
    SideRight = AVChannel.AV_CHAN_SIDE_RIGHT,
    TopCenter = AVChannel.AV_CHAN_TOP_CENTER,
    TopFrontLeft = AVChannel.AV_CHAN_TOP_FRONT_LEFT,
    TopFrontCenter = AVChannel.AV_CHAN_TOP_FRONT_CENTER,
    TopFrontRight = AVChannel.AV_CHAN_TOP_FRONT_RIGHT,
    TopBackLeft = AVChannel.AV_CHAN_TOP_BACK_LEFT,
    TopBackCenter = AVChannel.AV_CHAN_TOP_BACK_CENTER,
    TopBackRight = AVChannel.AV_CHAN_TOP_BACK_RIGHT,
    /** = AVChannel./** Stereo downmix. */
    StereoLeft = AVChannel.AV_CHAN_STEREO_LEFT,
    /** = AVChannel./** See above. */
    StereoRight = AVChannel.AV_CHAN_STEREO_RIGHT,
    WideLeft = AVChannel.AV_CHAN_WIDE_LEFT,
    WideRight = AVChannel.AV_CHAN_WIDE_RIGHT,
    SurroundDirectLeft = AVChannel.AV_CHAN_SURROUND_DIRECT_LEFT,
    SurroundDirectRight = AVChannel.AV_CHAN_SURROUND_DIRECT_RIGHT,
    LowFrequency2 = AVChannel.AV_CHAN_LOW_FREQUENCY_2,
    TopSideLeft = AVChannel.AV_CHAN_TOP_SIDE_LEFT,
    TopSideRight = AVChannel.AV_CHAN_TOP_SIDE_RIGHT,
    BottomFrontCenter = AVChannel.AV_CHAN_BOTTOM_FRONT_CENTER,
    BottomFrontLeft = AVChannel.AV_CHAN_BOTTOM_FRONT_LEFT,
    BottomFrontRight = AVChannel.AV_CHAN_BOTTOM_FRONT_RIGHT,

    /// <summary> Channel is empty can be safely skipped. </summary>
    Unused = 0x200,

    /// <summary> Channel contains data, but its position is unknown. </summary>
    Unknown = 0x300,

    /// <summary>
    /// Range of channels between <see cref="AmbisonicBase"/> and
    /// <see cref="AmbisonicEnd"/> represent Ambisonic components using the ACN system.
    /// <para/>
    /// Given a channel id `i` between <see cref="AmbisonicBase"/> and
    /// <see cref="AmbisonicEnd"/> (inclusive), the ACN index of the channel `n` is
    /// `n = i - <see cref="AmbisonicBase"/>`.
    /// </summary>
    /// <remarks>
    /// These values are only used for <see cref="ChannelOrder.Custom"/> channel
    /// orderings, the <see cref="ChannelOrder.Ambisonic"/> ordering orders the channels
    /// implicitly by their position in the stream.
    /// </remarks>
    AmbisonicBase = 0x400,
    // leave space for 1024 ids, which correspond to maximum order-32 harmonics,
    // which should be enough for the foreseeable use cases
    AmbisonicEnd = 0x7ff,
}
public enum ChannelOrder
{
    /// <summary>
    /// Only the channel count is specified, without any further information
    /// about the channel order.
    /// </summary>
    Unspecified = AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC,
    /// <summary>
    /// The native channel order, i.e. the channels are in the same order in
    /// which they are defined in the AVChannel enum. This supports up to 63
    /// different channels.
    /// </summary>
    Native = AVChannelOrder.AV_CHANNEL_ORDER_NATIVE,
    /// <summary>
    /// The channel order does not correspond to any other predefined order and
    /// is stored as an explicit map. For example, this could be used to support
    /// layouts with 64 or more channels, or with empty/skipped (AV_CHAN_SILENCE)
    /// channels at arbitrary positions.
    /// </summary>
    Custom = AVChannelOrder.AV_CHANNEL_ORDER_CUSTOM,
    /// <summary>
    /// The audio is represented as the decomposition of the sound field into
    /// spherical harmonics. Each channel corresponds to a single expansion
    /// component. Channels are ordered according to ACN (Ambisonic Channel
    /// Number).
    /// <para/>
    /// The channel with the index n in the stream contains the spherical
    /// harmonic of degree l and order m given by
    /// <code>
    ///   l   = floor(sqrt(n)),
    ///   m   = n - l * (l + 1).
    /// </code>
    /// <para/>
    /// Conversely given a spherical harmonic of degree l and order m, the
    /// corresponding channel index n is given by
    /// <code>
    ///   n = l * (l + 1) + m.
    /// </code>
    /// <para/>
    /// Normalization is assumed to be SN3D (Schmidt Semi-Normalization)
    /// as defined in AmbiX format $ 2.1.
    /// </summary>
    Ambisonic = AVChannelOrder.AV_CHANNEL_ORDER_AMBISONIC,
}