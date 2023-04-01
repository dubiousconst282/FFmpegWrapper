namespace FFmpeg.Wrapper;

public unsafe class VideoDecoder : MediaDecoder
{
    public int Width => _ctx->width;
    public int Height => _ctx->height;
    public AVPixelFormat PixelFormat => _ctx->pix_fmt;

    public PictureFormat FrameFormat => new(Width, Height, PixelFormat);

    public VideoDecoder(AVCodecContext* ctx)
        : base(ctx, AVMediaType.AVMEDIA_TYPE_VIDEO) { }

    public VideoDecoder(AVCodecID codec)
        : base(codec, AVMediaType.AVMEDIA_TYPE_VIDEO) { }

    //Used to prevent callback pointer from being GC collected
    AVCodecContext_get_format? _chooseHwPixelFmt;

    public void SetupHardwareAccelerator(HardwareDevice device, params AVPixelFormat[] preferredPixelFormats)
    {
        ThrowIfOpen();

        _ctx->hw_device_ctx = ffmpeg.av_buffer_ref(device.Handle);
        _ctx->get_format = _chooseHwPixelFmt = (ctx, pAvailFmts) => {
            for (var pFmt = pAvailFmts; *pFmt != PixelFormats.None; pFmt++) {
                if (Array.IndexOf(preferredPixelFormats, *pFmt) >= 0) {
                    return *pFmt;
                }
            }
            return PixelFormats.None;
        };
    }

    /// <summary> Returns a new list containing all hardware acceleration configurations marked with `AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX`. </summary>
    public List<HWConfigDesc> GetHardwareConfigs()
    {
        var configs = new List<HWConfigDesc>();

        for (int i = 0; ; i++) {
            var config = ffmpeg.avcodec_get_hw_config(_ctx->codec, i);
            if (config == null) break;
            
            //AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX
            if ((config->methods & 0x01) == 0) continue;

            configs.Add(new() {
                DeviceType = config->device_type,
                PixelFormat = config->pix_fmt,
            });
        }
        return configs;
    }


    public readonly struct HWConfigDesc
    {
        public AVPixelFormat PixelFormat { get; init; }
        public AVHWDeviceType DeviceType { get; init; }
    }
}