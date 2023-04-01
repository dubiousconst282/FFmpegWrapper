namespace GL2O;

/// <summary> Represents a shader program. </summary>
public class ShaderProgram : GLObject
{
    public int Id { get; private set; }
    public string? Name { get; set; }

    Dictionary<string, int> _uniformLocs = new();

    public ShaderProgram(string? name = null)
    {
        Id = GL.CreateProgram();
        Name = name;
    }

    public void DrawArrays(
        PrimitiveType mode, VertexFormat format, BufferObject vertices,
        int offset, int count,
        int numInstances = 1, int baseInstance = 0)
    {
        GL.UseProgram(Id);
        GL.BindVertexArray(format.Id);
        GL.VertexArrayVertexBuffer(format.Id, 0, vertices.Id, 0, format.Stride);
        GL.DrawArraysInstancedBaseInstance(mode, offset, count, numInstances, baseInstance);
    }
    public void DrawElements(
        PrimitiveType mode, DrawElementsType indexType, 
        VertexFormat format, BufferObject vertices, BufferObject indices,
        int offset, int count, 
        int baseVertex = 0, int numInstances = 1, int baseInstance = 0)
    {
        GL.UseProgram(Id);
        GL.BindVertexArray(format.Id);
        GL.VertexArrayVertexBuffer(format.Id, 0, vertices.Id, 0, format.Stride);
        GL.VertexArrayElementBuffer(format.Id, indices.Id);
        GL.DrawElementsInstancedBaseVertexBaseInstance(
            mode, count, indexType, offset * format.Stride,
            numInstances, baseVertex, baseInstance);
    }

    public void SetUniform(string name, int value) => GL.ProgramUniform1(Id, GetUniformLocation(name), value);
    public void SetUniform(string name, float value) => GL.ProgramUniform1(Id, GetUniformLocation(name), value);
    public void SetUniform(string name, in Vector3 value) => GL.ProgramUniform3(Id, GetUniformLocation(name), value.X, value.Y, value.Z);
    public void SetUniform(string name, in Matrix4x4 value, bool transpose = false) => GL.ProgramUniformMatrix4(Id, GetUniformLocation(name), 1, transpose, ref Unsafe.AsRef(in value.M11));
    public void SetUniform(string name, in OpenTK.Mathematics.Matrix3 value, bool transpose = false) => GL.ProgramUniformMatrix3(Id, GetUniformLocation(name), 1, transpose, ref Unsafe.AsRef(in value.Row0.X));

    public int GetUniformLocation(string name)
    {
        ref int loc = ref CollectionsMarshal.GetValueRefOrAddDefault(_uniformLocs, name, out bool exists);
        if (!exists) {
            loc = GL.GetUniformLocation(Id, name);
        }
        return loc;
    }
    public int GetAttribLocation(string name)
    {
        return GL.GetAttribLocation(Id, name);
    }

    public void AttachFile(ShaderType type, string filename)
        => Attach(type, File.ReadAllText(filename));

    public void Attach(ShaderType type, string source)
    {
        int shaderId = GL.CreateShader(type);
        GL.ShaderSource(shaderId, source);

        GL.CompileShader(shaderId);
        GL.GetShader(shaderId, ShaderParameter.CompileStatus, out int compileStatus);

        if (compileStatus != (int)All.True) {
            string infoLog = GL.GetShaderInfoLog(shaderId);
            throw new InvalidOperationException($"Failed to attach {type} shader to program '{Name ?? Id.ToString()}':\n\n{infoLog}");
        }
        GL.AttachShader(Id, shaderId);
    }

    public void Link()
    {
        GL.LinkProgram(Id);
        DeleteAttachedShaders();

        GL.GetProgram(Id, GetProgramParameterName.LinkStatus, out int linkStatus);

        if (linkStatus != (int)All.True) {
            string infoLog = GL.GetShaderInfoLog(Id);
            throw new InvalidOperationException($"Failed to link shader program '{Name ?? Id.ToString()}':\n\n{infoLog}");
        }
    }
    private void DeleteAttachedShaders()
    {
        Span<int> shaders = stackalloc int[16];
        int count = 1;

        while (count > 0) {
            GL.GetAttachedShaders(Id, 16, out count, out shaders[0]);

            foreach (int shaderId in shaders.Slice(0, count)) {
                GL.DetachShader(Id, shaderId);
                GL.DeleteShader(shaderId);
            }
        }
    }

    public void Dispose()
    {
        if (Id != 0) {
            GL.DeleteProgram(Id);
            Id = 0;
        }
    }
}