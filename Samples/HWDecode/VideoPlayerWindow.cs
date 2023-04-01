using FFmpeg.Wrapper;
using System.Text;

using GL2O;

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using FFmpeg.AutoGen;
using System.Diagnostics;

public unsafe class VideoPlayerWindow : GameWindow
{
    private MediaDemuxer _demuxer;
    private MediaStream _stream;
    private VideoDecoder _decoder;
    private VideoFrame _frame = new();
    private MediaPacket _packet = new();

    private Shader _shader = null!;
    private Texture2D _textureY = null!, _textureUV = null!;
    private VertexFormat _format = null!;
    private BufferObject _emptyVbo = null!;

    private double _time = 0;

    public VideoPlayerWindow(string videoPath)
        : base(
            new GameWindowSettings(),
            new NativeWindowSettings() {
                Size = new(1280, 720),
                Flags = ContextFlags.Debug | ContextFlags.ForwardCompatible,
                APIVersion = new Version(4, 5)
            })
    {
        _demuxer = new MediaDemuxer(videoPath);

        _stream = _demuxer.FindBestStream(MediaTypes.Video)!;
        _decoder = (VideoDecoder)_demuxer.CreateStreamDecoder(_stream, open: false);

        var bestConfig = _decoder.GetHardwareConfigs()
            .DefaultIfEmpty()
            .MaxBy(config => config.DeviceType switch {
                HWDeviceTypes.VAAPI => 100,
                //HWDeviceTypes.D3D11VA   => 100,   no map() support as of 6.0
                HWDeviceTypes.DXVA2 => 90,
                HWDeviceTypes.None => 0,
                _ => -1,  //Ignore unknown accelerators for now
            });

        //HWDevice is ref-counted, it's ok to dispose of it here.
        using var device = HardwareDevice.Alloc(bestConfig.DeviceType);

        if (device != null) {
            _decoder.SetupHardwareAccelerator(device, bestConfig.PixelFormat);
        }
        _decoder.Open();

        RenderFrequency = ffmpeg.av_q2d(_stream.Handle->avg_frame_rate);
        UpdateFrequency = 5; //don't waste CPU on update logic
    }

    // Now, we start initializing OpenGL.
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

        _shader = new Shader("Full Screen Texture");

        string basePath = AppContext.BaseDirectory + "Resources/shaders/";
        _shader.AttachFile(ShaderType.VertexShader, basePath + "full_screen_quad.vert");
        _shader.AttachFile(ShaderType.FragmentShader, basePath + "solid_texture_y_uv.frag");
        _shader.Link();

        _format = VertexFormat.FromStruct<EmptyVertex>(_shader);
        _emptyVbo = new BufferObject();
        _emptyVbo.SetData<byte>(new byte[16]);

        _textureY = new Texture2D(_decoder.Width, _decoder.Height, 1, SizedInternalFormat.R8);
        _textureUV = new Texture2D(_decoder.Width / 2, _decoder.Height / 2, 1, SizedInternalFormat.Rg8);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        //Decode next frame  
        while (_demuxer.Read(_packet)) {
            if (_packet.StreamIndex != _stream.Index) continue;
            _decoder.SendPacket(_packet);

            if (_decoder.ReceiveFrame(_frame)) {
                if (!_frame.IsHardwareFormat) {
                    throw new NotImplementedException();
                }

                //There's no easy way to interop HW and GL surfaces, so we'll 
                //have to do a copy which possibly trips through the CPU here.
                //This seems to be quite fast with my iGPU, it will probably
                //be quite slower with an discrete GPU though.
                using var mapping = _frame.Map(HWFrameMapFlags.Read | HWFrameMapFlags.Direct);

                Debug.Assert(mapping.PixelFormat == AVPixelFormat.AV_PIX_FMT_NV12);

                _textureY.SetPixels<byte>(
                    mapping.GetPlaneSpan<byte>(0),
                    0, 0, _frame.Width, _frame.Height,
                    PixelFormat.Red, PixelType.UnsignedByte, rowLength: mapping.Strides[0]);

                _textureUV.SetPixels<byte>(
                    mapping.GetPlaneSpan<byte>(1), 
                    0, 0, _frame.Width / 2, _frame.Height / 2,
                    PixelFormat.Rg, PixelType.UnsignedByte, rowLength: mapping.Strides[1] / 2);
                break;
            }
        }

        //Render
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _textureY.BindUnit(0);
        _textureUV.BindUnit(1);
        _shader.SetUniform("u_TextureY", 0);
        _shader.SetUniform("u_TextureUV", 1);
        _shader.DrawArrays(PrimitiveType.Triangles, _format, _emptyVbo, 0, 3);

        SwapBuffers();
        _time += e.Time;
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        if (KeyboardState.IsKeyDown(Keys.Escape)) {
            Close();
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, Size.X, Size.Y);
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        _decoder.Dispose();
        _demuxer.Dispose();

        _shader.Dispose();
        _emptyVbo.Dispose();
        _textureY.Dispose();
        _textureUV.Dispose();
        _format.Dispose();
    }

    struct EmptyVertex { }
}