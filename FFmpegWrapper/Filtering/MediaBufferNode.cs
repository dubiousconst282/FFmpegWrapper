namespace FFmpeg.Wrapper;

public unsafe class MediaBufferSource : MediaFilterNode
{
    internal MediaBufferSource(AVFilterContext* handle)
        : base(handle) { }

    public void SendFrame(MediaFrame? frame)
    {
        ffmpeg.av_buffersrc_write_frame(Handle, frame == null ? null : frame.Handle).CheckError();
    }
}

public abstract unsafe class MediaBufferSink : MediaFilterNode
{
    protected MediaBufferSink(AVFilterContext* handle)
        : base(handle) { }

    public Rational TimeBase => ffmpeg.av_buffersink_get_time_base(Handle);

    /// <summary> Gets a frame with filtered data from the sink, if one is available. </summary>
    /// <param name="onlyIfBuffered"> If true, don't run the filter graph and return a null frame if there are none buffered in the sink. </param> 
    protected AVFrame* ReceiveFrame(bool onlyIfBuffered)
    {
        // NOTE: av_buffersink_get_frame() will leak memory if the output frame is not empty.
        var frame = ffmpeg.av_frame_alloc();
        var result = (LavResult)ffmpeg.av_buffersink_get_frame_flags(Handle, frame, onlyIfBuffered ? ffmpeg.AV_BUFFERSINK_FLAG_NO_REQUEST : 0);

        if (result < 0) {
            ffmpeg.av_frame_free(&frame);
        }
        if (result is not (LavResult.Success or LavResult.TryAgain or LavResult.EndOfFile)) {
            Helpers.ThrowError((int)result, "Could not get output frame from filter graph");
        }
        return result < 0 ? null : frame;
    }

    /// <summary> Gets a frame with filtered data from the sink, if one is available. </summary>
    /// <param name="onlyIfBuffered"> If true, don't run the filter graph and return a null frame if there are none buffered in the sink. </param> 
    public bool ReceiveFrame(MediaFrame frame, bool onlyIfBuffered = false)
    {
        // NOTE: av_buffersink_get_frame() will leak memory if the output frame is not empty.
        ffmpeg.av_frame_unref(frame.Handle);
        var result = (LavResult)ffmpeg.av_buffersink_get_frame_flags(Handle, frame.Handle, onlyIfBuffered ? ffmpeg.AV_BUFFERSINK_FLAG_NO_REQUEST : 0);
        return result.IsSuccess();
    }
}

public unsafe class AudioBufferSink : MediaBufferSink
{
    internal AudioBufferSink(AVFilterContext* handle)
        : base(handle) { }

    /// <summary> Gets the output audio format. The returned value is undefined if the filter graph is not configured. </summary>
    public AudioFormat Format {
        get {
            int fmt = ffmpeg.av_buffersink_get_format(Handle);
            int rate = ffmpeg.av_buffersink_get_sample_rate(Handle);

            AVChannelLayout nativeLayout;
            ffmpeg.av_buffersink_get_ch_layout(Handle, &nativeLayout);
            var layout = ChannelLayout.FromExisting(&nativeLayout);
            ffmpeg.av_channel_layout_uninit(&nativeLayout);
            
            return new AudioFormat((AVSampleFormat)fmt, rate, layout);
        }
    }

    /// <inheritdoc cref="MediaBufferSink.ReceiveFrame(bool)"/>
    [Obsolete("Prefer to use `MediaBufferSink.ReceiveFrame(MediaFrame, bool)` instead.")]
    public new AudioFrame? ReceiveFrame(bool onlyIfBuffered = false)
    {
        var frame = base.ReceiveFrame(onlyIfBuffered);
        return frame == null ? null : new AudioFrame(frame, takeOwnership: true);
    }
}

public unsafe class VideoBufferSink : MediaBufferSink
{
    internal VideoBufferSink(AVFilterContext* handle)
        : base(handle) { }

    /// <summary> Gets the output video format. The returned value is undefined if the filter graph is not configured. </summary>
    public PictureFormat Format {
        get {
            int fmt = ffmpeg.av_buffersink_get_format(Handle);
            int width = ffmpeg.av_buffersink_get_w(Handle);
            int height = ffmpeg.av_buffersink_get_h(Handle);
            var pixelAspect = ffmpeg.av_buffersink_get_sample_aspect_ratio(Handle);
            return new PictureFormat(width, height, (AVPixelFormat)fmt, pixelAspect);
        }
    }
    public Rational FrameRate {
        get {
            return ffmpeg.av_buffersink_get_frame_rate(Handle);
        }
    }

    /// <inheritdoc cref="MediaBufferSink.ReceiveFrame(bool)"/>
    [Obsolete("Prefer to use `MediaBufferSink.ReceiveFrame(MediaFrame, bool)` instead.")]
    public new VideoFrame? ReceiveFrame(bool onlyIfBuffered = false)
    {
        var frame = base.ReceiveFrame(onlyIfBuffered);
        return frame == null ? null : new VideoFrame(frame, takeOwnership: true);
    }
}