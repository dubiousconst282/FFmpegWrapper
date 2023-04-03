namespace GL2O;

public unsafe class BufferObject : GLObject
{
    public int Id { get; private set; }

    public long Size {
        get {
            GL.GetNamedBufferParameter(Id, BufferParameterName.BufferSize, out long size);
            return size;
        }
    }

    public BufferObject()
    {
        GL.CreateBuffers(1, out int id);
        Id = id;
    }
    public BufferObject(nint size, BufferStorageFlags flags, ReadOnlySpan<byte> data = default)
    {
        GL.CreateBuffers(1, out int id);
        GL.NamedBufferStorage(id, size, ref MemoryMarshal.GetReference(data), flags);
        Id = id;
    }


    public void SetData<T>(ReadOnlySpan<T> data, BufferUsageHint usage = BufferUsageHint.StaticDraw) where T : unmanaged
    {
        GL.NamedBufferData(Id, (nint)data.Length * sizeof(T), ref MemoryMarshal.GetReference(data), usage);
    }
    public void SetDataRange<T>(ReadOnlySpan<T> data, nint offset = 0) where T : unmanaged
    {
        GL.NamedBufferSubData(Id, offset * sizeof(T), (nint)data.Length * sizeof(T), ref MemoryMarshal.GetReference(data));
    }
    public void GetDataRange<T>(Span<T> data, nint offset = 0) where T : unmanaged
    {
        GL.GetNamedBufferSubData(Id, offset * sizeof(T), data.Length * sizeof(T), ref MemoryMarshal.GetReference(data));
    }

    /// <summary> Creates a memory mapping for this buffer's data store. </summary>
    /// <remarks> The returned span is valid until <see cref="Unmap"/> is called. It should not be accessed afterwards. </remarks>
    public Span<T> Map<T>(nint offset, int count, BufferAccessMask access) where T : unmanaged
    {
        var ptr = MapPtr(offset, (nint)count * sizeof(T), access);
        return new Span<T>(ptr, count);
    }
    public byte* MapPtr(nint offset, nint count, BufferAccessMask access)
    {
        return (byte*)GL.MapNamedBufferRange(Id, offset, count, access);
    }
    public void Unmap()
    {
        GL.UnmapNamedBuffer(Id);
    }

    public void Dispose()
    {
        if (Id != 0) {
            GL.DeleteBuffer(Id);
            Id = 0;
        }
    }
}