namespace FFmpeg.Wrapper;

public unsafe abstract class MediaFrame : FFObject
{
    protected AVFrame* _frame;
    protected bool _ownsFrame = true;

    public AVFrame* Handle {
        get {
            ThrowIfDisposed();
            return _frame;
        }
    }

    /// <inheritdoc cref="AVFrame.best_effort_timestamp" />
    public long? BestEffortTimestamp => Helpers.GetPTS(_frame->best_effort_timestamp);

    /// <inheritdoc cref="AVFrame.pts" />
    public long? PresentationTimestamp {
        get => Helpers.GetPTS(_frame->pts);
        set => Helpers.SetPTS(ref _frame->pts, value);
    }

    /// <summary> Duration of the frame, in the same units as <see cref="PresentationTimestamp"/>. Null if unknown. </summary>
    public long? Duration {
        get => _frame->duration > 0 ? _frame->duration : null;
        set => _frame->duration = value ?? 0;
    }

    /// <inheritdoc cref="AVFrame.side_data"/>
    public FrameSideDataList SideData => new(_frame);

    protected override void Free()
    {
        if (_frame != null && _ownsFrame) {
            fixed (AVFrame** ppFrame = &_frame) {
                ffmpeg.av_frame_free(ppFrame);
            }
        }
        _frame = null;
    }
    protected void ThrowIfDisposed()
    {
        if (_frame == null) {
            throw new ObjectDisposedException(nameof(MediaFrame));
        }
    }
}