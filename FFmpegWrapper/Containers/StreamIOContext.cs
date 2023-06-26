namespace FFmpeg.Wrapper;

internal class StreamIOContext : IOContext
{
    readonly Stream _stream;
    readonly bool _leaveOpen;

    public StreamIOContext(Stream stream, bool read, bool leaveOpen, int bufferSize)
        : base(bufferSize, canRead: read, canWrite: !read, stream.CanSeek)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
    }

#if NETSTANDARD2_1_OR_GREATER
    protected override int Read(Span<byte> buffer) => _stream.Read(buffer);
    protected override void Write(ReadOnlySpan<byte> buffer) => _stream.Write(buffer);
#else
    private readonly byte[] _scratchBuffer = new byte[4096 * 4];

    protected override int Read(Span<byte> buffer)
    {
        int bytesRead = _stream.Read(_scratchBuffer, 0, Math.Min(buffer.Length, _scratchBuffer.Length));
        _scratchBuffer.AsSpan(0, bytesRead).CopyTo(buffer);
        return bytesRead;
    }
    protected override void Write(ReadOnlySpan<byte> buffer)
    {
        int pos = 0;
        while (pos < buffer.Length) {
            int count = Math.Min(_scratchBuffer.Length, buffer.Length - pos);
            buffer.Slice(pos, count).CopyTo(_scratchBuffer);
            _stream.Write(_scratchBuffer, 0, count);
            pos += count;
        }
    }
#endif

    protected override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    protected override long? GetLength()
    {
        try {
            return _stream.Length;
        } catch (NotSupportedException) {
            return null;
        }
    }

    protected override void Free()
    {
        base.Free();

        if (!_leaveOpen) {
            _stream.Dispose();
        }
    }
}