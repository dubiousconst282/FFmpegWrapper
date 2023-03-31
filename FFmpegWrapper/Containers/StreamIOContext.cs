namespace FFmpeg.Wrapper;

/// <summary> Wraps a <see cref="Stream"/> into a <see cref="AVIOContext"/>. </summary>
public class StreamIOContext : IOContext
{
    public Stream BaseStream { get; }

    private bool _leaveOpen;

    public StreamIOContext(Stream stream, bool leaveOpen = false, int bufferSize = 4096)
        : base(bufferSize, stream.CanRead, stream.CanWrite, stream.CanSeek)
    {
        BaseStream = stream;
        _leaveOpen = leaveOpen;
    }

    protected override int Read(Span<byte> buffer) => BaseStream.Read(buffer);
    protected override void Write(ReadOnlySpan<byte> buffer) => BaseStream.Write(buffer);
    protected override long Seek(long offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);

    protected override long? GetLength()
    {
        try {
            return BaseStream.Length;
        } catch (NotSupportedException) {
            return null;
        }
    }

    protected override void Free()
    {
        base.Free();

        if (!_leaveOpen) {
            BaseStream.Dispose();
        }
    }
}