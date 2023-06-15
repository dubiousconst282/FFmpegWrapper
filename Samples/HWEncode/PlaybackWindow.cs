using FFmpeg.Wrapper;
using System.Text;

using GL2O;

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using FFmpeg.AutoGen;
using System.Diagnostics;

public unsafe class PlaybackWindow : GameWindow
{
    private MediaMuxer _muxer;
    private MediaStream _stream;
    private VideoEncoder _encoder;
    private VideoFrame _rgbFrame, _frame = new();
    private SwScaler _scaler;

    private List<ShaderProgram> _shaders = new();
    private VertexFormat _format = null!;
    private BufferObject _emptyVbo = null!;
    private BufferObject[] _pbos = new BufferObject[4];

    private int _frameNo;
    const int _width = 1280, _height = 720;

    public PlaybackWindow(string outVideoPath)
        : base(
            new GameWindowSettings(),
            new NativeWindowSettings() {
                Size = new(_width, _height),
                Flags = ContextFlags.Debug | ContextFlags.ForwardCompatible,
                APIVersion = new Version(4, 5),
            })
    {
        UpdateFrequency = 5; //don't waste CPU on update logic

        var format = new PictureFormat(_width, _height, PixelFormats.NV12);

        using var device = VideoEncoder.CreateCompatibleHardwareDevice(CodecIds.H264, format, out var hwConfig)
            ?? throw new InvalidOperationException("No compatible hardware encoder for the given settings");

        //These renders look terribly blocky and artifacty even at relatively high bitrates for some reason (even with libx264).
        _encoder = new VideoEncoder(hwConfig, format, frameRate: 60, bitrate: 12000_000, device, null);

        if (_encoder.Codec.Name == "h264_qsv") {
            _encoder.SetOption("preset", "veryslow");
            _encoder.SetOption("look_ahead", "1");
        }

        _muxer = new MediaMuxer(outVideoPath);
        _stream = _muxer.AddStream(_encoder);
        _muxer.Open();

        _rgbFrame = new VideoFrame(format.Width, format.Height, PixelFormats.BGRA);
        _frame = new VideoFrame(format);
        _scaler = new SwScaler(_rgbFrame.Format, format); //TODO: do RGB->YUV conversion in shader (actually QSV accepts BGRA)
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.DebugMessageCallback((source, type, id, severity, length, ptext, _) => {
            var severityStr = severity.ToString().Substring("DebugSeverity".Length).ToUpper();
            var sourceStr = source.ToString().Substring("DebugSource".Length);
            var typeStr = type.ToString().Substring("DebugType".Length);
            var text = Encoding.UTF8.GetString((byte*)ptext, length);

            Console.WriteLine($"GL-{sourceStr}-{typeStr}: [{severityStr}] {text}");
        }, 0);
        GL.Enable(EnableCap.DebugOutput);

        string shaderBasePath = AppContext.BaseDirectory + "shaders/";

        foreach (string file in Directory.GetFiles(shaderBasePath + "shadertoy/", "*.frag")) {
            Console.WriteLine("Loading shader " + Path.GetFileName(file));

            var shader = new ShaderProgram();
            shader.AttachFile(ShaderType.VertexShader, shaderBasePath + "full_screen_quad.vert");
            shader.AttachFile(ShaderType.FragmentShader, file);
            shader.Link();

            _shaders.Add(shader);
        }

        _format = VertexFormat.CreateEmpty();

        _emptyVbo = new BufferObject(16, BufferStorageFlags.None);

        for (int i = 0; i < _pbos.Length; i++) {
            _pbos[i] = new BufferObject(_width * _height * 4, BufferStorageFlags.DynamicStorageBit | BufferStorageFlags.MapReadBit);
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        GL.Viewport(0, 0, _width, _height);

        double time = _frameNo / ffmpeg.av_q2d(_encoder.FrameRate);
        int demoDuration = 5;

        //Encode some previously rendered frame
        if (_frameNo >= _pbos.Length) {
            EncodeFrame();
        }
        if (time > _shaders.Count * demoDuration) {
            Close();
        }

        //Draw demo shader
        var currShader = _shaders[(int)time / demoDuration % _shaders.Count];
        currShader.SetUniform("iTime", (float)time);
        currShader.SetUniform("iResolution", new Vector3(ClientSize.X, ClientSize.Y, 0));
        currShader.DrawArrays(PrimitiveType.Triangles, _format, _emptyVbo, 0, 3);

        //Begin the download of the current frame to one of our PBOs
        BeginBackbufferDownload();

        SwapBuffers();
        _frameNo++;
    }

    private void BeginBackbufferDownload()
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
        GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbos[_frameNo % _pbos.Length].Id);
        GL.ReadPixels(0, 0, _width, _height, PixelFormat.Bgra, PixelType.UnsignedByte, 0);
        GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
    }
    private void EncodeFrame()
    {
        var pbo = _pbos[(_frameNo - _pbos.Length + 1) % _pbos.Length];

        long start = Stopwatch.GetTimestamp();

        //OpenGL uses bottom-left as the pixel origin.
        //We could flip Y coords in the shader, but that'd move the issue to the
        //window framebuffer.  Just do it in the obvious way here.
        //
        //Note that giving the mapped memory directly to swscaler is _super_ slow.
        //It's probably doing lots of accesses for interpolation and planar chroma
        //packing or something like that.
        var mapping = pbo.Map<byte>(0, (int)pbo.Size, BufferAccessMask.MapReadBit);

        for (int y = 0; y < _height; y++) {
            var row = _rgbFrame.GetRowSpan<byte>(_height - 1 - y);
            mapping.Slice(y * row.Length, row.Length).CopyTo(row);
        }
        pbo.Unmap();
        Console.WriteLine("Read " + Stopwatch.GetElapsedTime(start).TotalMilliseconds + "ms");

        start = Stopwatch.GetTimestamp();
        _scaler.Convert(_rgbFrame, _frame);
        Console.WriteLine("Convert " + Stopwatch.GetElapsedTime(start).TotalMilliseconds + "ms");

        _frame.PresentationTimestamp = _encoder.GetFramePts(_frameNo);
        _muxer.EncodeAndWrite(_stream, _encoder, _frame);
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        //Flush delayed frames  
        _muxer.EncodeAndWrite(_stream, _encoder, null);
        _muxer.Dispose();

        _encoder.Dispose();
        _frame.Dispose();
        _scaler.Dispose();

        foreach (var shader in _shaders) {
            shader.Dispose();
        }
        foreach (var pbo in _pbos) {
            pbo.Dispose();
        }
        _emptyVbo.Dispose();
        _format.Dispose();
    }
}