namespace FFmpeg.Wrapper;

public unsafe class HardwareFramePool : FFObject
{
    private AVBufferRef* _ctx;

    public AVBufferRef* Handle {
        get {
            ThrowIfDisposed();
            return _ctx;
        }
    }
    public AVHWFramesContext* RawHandle {
        get {
            ThrowIfDisposed();
            return (AVHWFramesContext*)_ctx->data;
        }
    }

    public int Width => RawHandle->width;
    public int Height => RawHandle->height;

    /// <inheritdoc cref="AVHWFramesContext.format" />
    public AVPixelFormat HwFormat => RawHandle->format;

    /// <inheritdoc cref="AVHWFramesContext.sw_format" />
    public AVPixelFormat SwFormat => RawHandle->sw_format;

    public HardwareFramePool(AVBufferRef* deviceCtx)
    {
        _ctx = deviceCtx;
    }

    /// <summary> Allocate a new frame attached to the current hardware frame pool. </summary>
    public VideoFrame AllocFrame()
    {
        var frame = ffmpeg.av_frame_alloc();
        int err = ffmpeg.av_hwframe_get_buffer(_ctx, frame, 0);
        if (err < 0) {
            ffmpeg.av_frame_free(&frame);
            err.ThrowError("Failed to allocate hardware frame");
        }
        return new VideoFrame(frame, takeOwnership: true);
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
            throw new ObjectDisposedException(nameof(HardwareFramePool));
        }
    }
}