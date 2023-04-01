namespace GL2O;

public class BufferObject : GLObject
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
        Id = GL.GenBuffer();
    }

    public unsafe void SetData<T>(ReadOnlySpan<T> data, BufferUsageHint usage = BufferUsageHint.StaticDraw) where T : unmanaged
    {
        GL.NamedBufferData(Id, data.Length * sizeof(T), ref MemoryMarshal.GetReference(data), usage);
    }

    public void Dispose()
    {
        if (Id != 0) {
            GL.DeleteBuffer(Id);
            Id = 0;
        }
    }
}