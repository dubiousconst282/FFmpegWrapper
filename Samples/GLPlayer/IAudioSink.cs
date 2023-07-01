using FFmpeg.Wrapper;

public interface IAudioSink : IDisposable
{
    AudioFormat Format { get; }

    public void Start();
    public void Stop();

    /// <summary> Returns a span to the next available buffer in the queue where samples can be written to. </summary>
    public Span<T> GetQueueBuffer<T>() where T : unmanaged;

    /// <summary> Releases the buffer space acquired in the previous call to <see cref="GetQueueBuffer{T}()"/>.</summary>
    public void AdvanceQueue(int numFramesWritten, bool replaceWithSilence = false);

    /// <summary> Blocks until the queued samples have been played and space is available. </summary>
    public void Wait();

    /// <summary> Gets the current playhead position, in samples, since <see cref="Start"/> was called. </summary>
    public long GetPosition();
}
