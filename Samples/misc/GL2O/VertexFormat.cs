namespace GL2O;

using System.Reflection;

public class VertexFormat : GLObject
{
    public int Id { get; private set; }
    public int Stride { get; }

    private VertexFormat(int stride)
    {
        GL.CreateVertexArrays(1, out int id);
        Id = id;
        Stride = stride;
    }

    public static VertexFormat CreateEmpty() => new VertexFormat(0);

    public static unsafe VertexFormat FromStruct<T>(ShaderProgram shader) where T : unmanaged
    {
        var vao = new VertexFormat(sizeof(T));
        var fields = typeof(T).GetFields()
            .Select(f => (
                Field: f,
                Layout: f.GetCustomAttribute<LayoutAttribute>()
                    ?? throw Error(f, "Missing required Layout attribute")
            ))
            .ToArray();

        for (int i = 0; i < fields.Length;) {
            var (field, layout) = fields[i];

            //Group fields targeting the same attribute, to allow for e.g.:
            //  [Layout(...)] public float x, y, z;
            int j = i + 1;
            while (j < fields.Length && fields[j].Layout.AttribName == layout.AttribName) j++;

            if (!s_KnownLayoutFormats.TryGetValue(field.FieldType, out var knownFormat)) {
                throw Error(field, "Unknown layout format type");
            }
            int location = shader.GetAttribLocation(layout.AttribName);

            if (location >= 0) {
                int offset = (int)Marshal.OffsetOf<T>(field.Name);

                int elemCount = layout.Count > 0 ? layout.Count : knownFormat.Count;
                var elemType = layout.Type > 0 ? layout.Type : knownFormat.ElemType;

                GL.EnableVertexArrayAttrib(vao.Id, location);
                GL.VertexArrayAttribBinding(vao.Id, location, 0);
                
                if (layout.Normalize) {
                    GL.VertexArrayAttribFormat(vao.Id, location, elemCount, elemType, true, offset);
                } else {
                    GL.VertexArrayAttribIFormat(vao.Id, location, elemCount, (VertexAttribIntegerType)elemType, offset);
                }
            }
            i = j;
        }
        return vao;

        static Exception Error(FieldInfo field, string msg)
            => throw new InvalidOperationException($"{msg} on struct field '{field.DeclaringType!.Name}.{field.Name}'");
    }

    public void Dispose()
    {
        if (Id != 0) {
            GL.DeleteVertexArray(Id);
            Id = 0;
        }
    }

#pragma warning disable format
    static readonly Dictionary<Type, (VertexAttribType ElemType, int Count)> s_KnownLayoutFormats = new() {
        { typeof(sbyte),        (VertexAttribType.Byte,         1) },
        { typeof(byte),         (VertexAttribType.UnsignedByte, 1) },

        { typeof(short),        (VertexAttribType.Short,        1) },
        { typeof(ushort),       (VertexAttribType.UnsignedShort,1) },

        { typeof(int),          (VertexAttribType.Int,          1) },
        { typeof(uint),         (VertexAttribType.UnsignedInt,  1) },

        { typeof(Half),         (VertexAttribType.HalfFloat,    1) },

        { typeof(float),        (VertexAttribType.Float,        1) },
        { typeof(Vector2),      (VertexAttribType.Float,        2) },
        { typeof(Vector3),      (VertexAttribType.Float,        3) },
        { typeof(Vector4),      (VertexAttribType.Float,        4) },
        { typeof(Matrix4x4),    (VertexAttribType.Float,        16) },
    };
#pragma warning restore format
}


[AttributeUsage(AttributeTargets.Field)]
public class LayoutAttribute : Attribute
{
    public string AttribName { get; }
    public VertexAttribType Type { get; set; } = 0;
    public int Count { get; set; } = 0;
    /// <summary> Normalizes source integer values to floats ranging [0..1] or [-1..1] for unsigned and signed types respectively. </summary>
    /// <remarks> If set to false, this will result in a call to glVertexArrayAttribIFormat(), which by effect assumes that the target attribute is integer-typed. </remarks>
    public bool Normalize { get; set; } = true;

    public LayoutAttribute(string attribName)
    {
        AttribName = attribName;
    }
}