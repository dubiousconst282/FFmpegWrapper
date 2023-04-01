namespace FFmpeg.Wrapper;

public unsafe class HardwareDevice : FFObject
{
    private AVBufferRef* _ctx;

    public AVBufferRef* Handle {
        get {
            ThrowIfDisposed();
            return _ctx;
        }
    }

    public AVHWDeviceType Type => ((AVHWDeviceContext*)Handle->data)->type;

    public HardwareDevice(AVBufferRef* deviceCtx)
    {
        _ctx = deviceCtx;
    }

    public static HardwareDevice? Alloc(AVHWDeviceType type)
    {
        AVBufferRef* ctx;
        if (ffmpeg.av_hwdevice_ctx_create(&ctx, type, null, null, 0) < 0) {
            return null;
        }
        return new HardwareDevice(ctx);
    }

    protected override void Free()
    {
        if (_ctx != null) {
            fixed (AVBufferRef** ppCtx = &_ctx) {
                ffmpeg.av_buffer_unref(ppCtx);
            }
        }
    }
    private void ThrowIfDisposed()
    {
        if (_ctx == null) {
            throw new ObjectDisposedException(nameof(HardwareDevice));
        }
    }
}