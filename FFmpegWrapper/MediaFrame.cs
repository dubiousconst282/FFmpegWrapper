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

    public long? BestEffortTimestamp => Helpers.GetPTS(_frame->best_effort_timestamp);
    public long? PresentationTimestamp {
        get => Helpers.GetPTS(_frame->pts);
        set => Helpers.SetPTS(ref _frame->pts, value);
    }

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