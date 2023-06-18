﻿namespace FFmpeg.Wrapper;

public unsafe class VideoEncoder : MediaEncoder
{
    public int Width {
        get => _ctx->width;
        set => SetOrThrowIfOpen(ref _ctx->width, value);
    }
    public int Height {
        get => _ctx->height;
        set => SetOrThrowIfOpen(ref _ctx->height, value);
    }
    public AVPixelFormat PixelFormat {
        get => _ctx->pix_fmt;
        set => SetOrThrowIfOpen(ref _ctx->pix_fmt, value);
    }

    public PictureFormat FrameFormat {
        get => new(Width, Height, PixelFormat);
        set {
            _ctx->width = value.Width;
            _ctx->height = value.Height;
            _ctx->pix_fmt = value.PixelFormat;
        }
    }

    /// <inheritdoc cref="AVCodecContext.gop_size"/>
    public int GopSize {
        get => _ctx->gop_size;
        set => SetOrThrowIfOpen(ref _ctx->gop_size, value);
    }
    /// <inheritdoc cref="AVCodecContext.max_b_frames"/>
    public int MaxBFrames {
        get => _ctx->max_b_frames;
        set => SetOrThrowIfOpen(ref _ctx->max_b_frames, value);
    }

    public int MinQuantizer {
        get => _ctx->qmin;
        set => SetOrThrowIfOpen(ref _ctx->qmin, value);
    }
    public int MaxQuantizer {
        get => _ctx->qmax;
        set => SetOrThrowIfOpen(ref _ctx->qmax, value);
    }

    public int CompressionLevel {
        get => _ctx->compression_level;
        set => SetOrThrowIfOpen(ref _ctx->compression_level, value);
    }

    public VideoEncoder(AVCodecID codecId, in PictureFormat format, double frameRate, int bitrate = 0)
        : this(MediaCodec.GetEncoder(codecId), format, frameRate, bitrate) { }

    public VideoEncoder(MediaCodec codec, in PictureFormat format, double frameRate, int bitrate = 0)
        : this(AllocContext(codec), takeOwnership: true)
    {
        FrameFormat = format;
        FrameRate = ffmpeg.av_d2q(frameRate, 100_000);
        TimeBase = ffmpeg.av_inv_q(FrameRate);
        BitRate = bitrate;
    }

    public VideoEncoder(AVCodecContext* ctx, bool takeOwnership)
        : base(ctx, MediaTypes.Video, takeOwnership) { }

    public VideoEncoder(CodecHardwareConfig config, in PictureFormat format, double frameRate, int bitrate, HardwareDevice device, HardwareFramePool? framePool = null)
        : this(config.Codec, in format, frameRate, bitrate)
    {
        SetHardwareContext(config, device, framePool);
    }

    /// <summary> Returns the correct <see cref="MediaFrame.PresentationTimestamp"/> for the given frame number, in respect to <see cref="CodecBase.FrameRate"/> and <see cref="CodecBase.TimeBase"/>. </summary>
    public long GetFramePts(long frameNumber)
    {
        return ffmpeg.av_rescale_q(frameNumber, ffmpeg.av_inv_q(FrameRate), TimeBase);
    }

    public static HardwareDevice? CreateCompatibleHardwareDevice(AVCodecID codecId, in PictureFormat format, out CodecHardwareConfig codecConfig)
    {
        foreach (var config in VideoEncoder.GetHardwareConfigs(codecId)) {
            if (config.PixelFormat != format.PixelFormat) continue;

            var device = HardwareDevice.Create(config.DeviceType);
            var constraints = device?.GetMaxFrameConstraints();

            if (device != null && (constraints == null || constraints.IsValidFormat(format))) {
                codecConfig = config;
                return device;
            }
            device?.Dispose();
        }
        codecConfig = default;
        return null;
    }

    public static List<CodecHardwareConfig> GetHardwareConfigs(AVCodecID? codecId = null, AVHWDeviceType? deviceType = null)
    {
        var configs = new List<CodecHardwareConfig>();

        void* iterState = null;
        AVCodec* codec;

        while ((codec = ffmpeg.av_codec_iterate(&iterState)) != null) {
            if ((codecId != null && codec->id != codecId) || ffmpeg.av_codec_is_encoder(codec) == 0) continue;

            int i = 0;
            AVCodecHWConfig* config;

            while ((config = ffmpeg.avcodec_get_hw_config(codec, i++)) != null) {
                const int reqMethods = (int)(CodecHardwareMethods.DeviceContext | CodecHardwareMethods.FramesContext);

                if ((config->methods & reqMethods) != 0 && (deviceType == null || config->device_type == deviceType)) {
                    configs.Add(new CodecHardwareConfig(codec, config));
                }
            }
        }
        return configs;
    }
}