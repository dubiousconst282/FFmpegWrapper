namespace FFmpeg.Wrapper;

public unsafe readonly struct CodecHardwareConfig
{
    public AVCodec* Codec { get; }
    public AVCodecHWConfig* Config { get; }

    public AVHWDeviceType DeviceType => Config->device_type;
    public AVPixelFormat PixelFormat => Config->pix_fmt;
    public CodecHardwareMethods Methods => (CodecHardwareMethods)Config->methods;

    public CodecHardwareConfig(AVCodec* codec, AVCodecHWConfig* config)
    {
        Codec = codec;
        Config = config;
    }

    public override string ToString() => new string((sbyte*)Codec->name) + " " + PixelFormat.ToString().Substring("AV_PIX_FMT_".Length);
}
[Flags]
public enum CodecHardwareMethods
{
    DeviceContext = 0x01,
    FramesContext = 0x02
}