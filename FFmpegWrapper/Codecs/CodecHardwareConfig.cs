namespace FFmpeg.Wrapper;

public unsafe readonly struct CodecHardwareConfig
{
    public AVCodecHWConfig* Handle { get; }
    public MediaCodec Codec { get; }

    public AVHWDeviceType DeviceType => Handle->device_type;
    public AVPixelFormat PixelFormat => Handle->pix_fmt;
    public CodecHardwareMethods Methods => (CodecHardwareMethods)Handle->methods;

    public CodecHardwareConfig(AVCodec* codec, AVCodecHWConfig* config)
    {
        Codec = new(codec);
        Handle = config;
    }

    public override string ToString() => Codec.Name + " " + PixelFormat.ToString().Substring("AV_PIX_FMT_".Length);
}
[Flags]
public enum CodecHardwareMethods
{
    /// <summary>
    /// The codec supports this format via the hw_device_ctx interface.
    /// <para/>
    /// When selecting this format, AVCodecContext.hw_device_ctx should
    /// have been set to a device of the specified type before calling
    /// avcodec_open2().
    /// </summary>
    DeviceContext = 0x01,

    /// <summary>
    /// The codec supports this format via the hw_frames_ctx interface.
    /// <para/>
    /// When selecting this format for a decoder,
    /// AVCodecContext.hw_frames_ctx should be set to a suitable frames
    /// context inside the get_format() callback.  The frames context
    /// must have been created on a device of the specified type.
    /// <para/>
    /// When selecting this format for an encoder,
    /// AVCodecContext.hw_frames_ctx should be set to the context which
    /// will be used for the input frames before calling avcodec_open2().
    /// </summary>
    FramesContext = 0x02,

    /// <summary>
    /// The codec supports this format by some internal method.
    /// <para/>
    /// This format can be selected without any additional configuration -
    /// no device or frames context is required.
    /// </summary>
    Internal = 0x04,
    
    /// <summary>
    /// The codec supports this format by some ad-hoc method.
    /// <para/>
    /// Additional settings and/or function calls are required.  See the
    /// codec-specific documentation for details.  (Methods requiring
    /// this sort of configuration are deprecated and others should be
    /// used in preference.)
    /// </summary>
    AdHoc = 0x08,
}