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

#if NETSTANDARD2_1_OR_GREATER
    protected override int Read(Span<byte> buffer) => BaseStream.Read(buffer);
    protected override void Write(ReadOnlySpan<byte> buffer) => BaseStream.Write(buffer);
#else
    private readonly byte[] _scratchBuffer = new byte[4096 * 4];

    protected override int Read(Span<byte> buffer)
    {
        int bytesRead = BaseStream.Read(_scratchBuffer, 0, Math.Min(buffer.Length, _scratchBuffer.Length));
        _scratchBuffer.AsSpan(0, bytesRead).CopyTo(buffer);
        return bytesRead;
    }
    protected override void Write(ReadOnlySpan<byte> buffer)
    {
        int pos = 0;
        while (pos < buffer.Length) {
            int count = Math.Min(_scratchBuffer.Length, buffer.Length - pos);
            buffer.Slice(pos, count).CopyTo(_scratchBuffer);
            BaseStream.Write(_scratchBuffer, 0, count);
            pos += count;
        }
    }
#endif

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