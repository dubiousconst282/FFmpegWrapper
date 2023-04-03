namespace FFmpeg.Wrapper;

public unsafe class VideoDecoder : MediaDecoder
{
    public int Width => _ctx->width;
    public int Height => _ctx->height;
    public AVPixelFormat PixelFormat => _ctx->pix_fmt;

    public PictureFormat FrameFormat => new(Width, Height, PixelFormat);

    public VideoDecoder(AVCodecID codecId)
        : this(FindCodecFromId(codecId, enc: false)) { }

    public VideoDecoder(AVCodec* codec)
        : this(AllocContext(codec)) { }

    public VideoDecoder(AVCodecContext* ctx, bool takeOwnership = true)
        : base(ctx, MediaTypes.Video, takeOwnership) { }

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
    public List<CodecHardwareConfig> GetHardwareConfigs()
    {
        ThrowIfDisposed();

        var configs = new List<CodecHardwareConfig>();

        int i = 0;
        AVCodecHWConfig* config;

        while ((config = ffmpeg.avcodec_get_hw_config(_ctx->codec, i++)) != null) {
            if ((config->methods & (int)CodecHardwareMethods.DeviceContext) != 0) {
                configs.Add(new CodecHardwareConfig(_ctx->codec, config));
            }
        }
        return configs;
    }
}