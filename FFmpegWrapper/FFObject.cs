namespace FFmpeg.Wrapper;

public abstract class FFObject : IDisposable
{
    public void Dispose()
    {
        Free();
        GC.SuppressFinalize(this);
    }
    ~FFObject() => Free();

    protected abstract void Free();
}