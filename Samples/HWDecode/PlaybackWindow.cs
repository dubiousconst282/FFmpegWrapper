using FFmpeg.Wrapper;
using System.Text;

using GL2O;

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using FFmpeg.AutoGen;
using System.Diagnostics;

using Matrix3x3 = OpenTK.Mathematics.Matrix3;

public unsafe class PlaybackWindow : GameWindow
{
    private MediaDemuxer _demuxer;
    private MediaStream _stream;
    private VideoDecoder _decoder;
    private VideoFrame _frame = new();
    private MediaPacket _packet = new();

    private ShaderProgram _shader = null!;
    private Texture2D _textureY = null!, _textureUV = null!;
    private VertexFormat _format = null!;
    private BufferObject _emptyVbo = null!;

    private double _avgDecodeTime;
    private TimeSpan _timestamp;

    public PlaybackWindow(string videoPath)
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
                _ => -1,  //Ignore unknown accelerators for now
            });

        //HWDevice is ref-counted, it's ok to dispose of it here.
        using var device = HardwareDevice.Create(bestConfig.DeviceType);

        if (device != null) {
            _decoder.SetupHardwareAccelerator(device, bestConfig.PixelFormat);
        }
        _decoder.Open();

        RenderFrequency = _stream.AvgFrameRate;
        UpdateFrequency = 5; //don't waste CPU on update logic
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

        _shader = new ShaderProgram("Full Screen Texture");
        _shader.AttachFile(ShaderType.VertexShader, shaderBasePath + "full_screen_quad.vert");
        _shader.AttachFile(ShaderType.FragmentShader, shaderBasePath + "solid_texture_y_uv.frag");
        _shader.Link();

        _format = VertexFormat.CreateEmpty();

        _emptyVbo = new BufferObject(16, BufferStorageFlags.None);

        _textureY = new Texture2D(_decoder.Width, _decoder.Height, 1, SizedInternalFormat.R8);
        _textureUV = new Texture2D(_decoder.Width / 2, _decoder.Height / 2, 1, SizedInternalFormat.Rg8);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        long startTime = Stopwatch.GetTimestamp();
        DecodeNextFrame();
        var decodeTime = Stopwatch.GetElapsedTime(startTime);

        _avgDecodeTime = _avgDecodeTime * 0.98 + decodeTime.TotalMilliseconds * 0.02;
        Title = $"Playing {_timestamp:mm\\:ss\\.ff} - DecodeTime: {_avgDecodeTime:0.00}ms (~{1000 / _avgDecodeTime:0} FPS)";

        //Render
        _textureY.BindUnit(0);
        _textureUV.BindUnit(1);
        _shader.SetUniform("u_TextureY", 0);
        _shader.SetUniform("u_TextureUV", 1);
        _shader.SetUniform("u_Yuv2RgbCoeffs", GetYuv2RgbCoeffsMatrix());
        _shader.DrawArrays(PrimitiveType.Triangles, _format, _emptyVbo, 0, 3);

        SwapBuffers();
    }

    private Matrix3x3 GetYuv2RgbCoeffsMatrix()
    {
        //TODO: consider colorspaces and stuff
        // - https://github.com/FFmpeg/FFmpeg/blob/925ac0da32697ef5853e90e0be56e106208099e2/libswscale/yuv2rgb.c
        // - https://github.com/FFmpeg/FFmpeg/blob/master/libavutil/csp.c#L46

        //https://en.wikipedia.org/wiki/YCbCr#ITU-R_BT.709_conversion
        return new Matrix3x3(
            1.0f,  0.0f,       1.5748f,
            1.0f, -0.187324f, -0.468124f,
            1.0f,  1.8556f,    0.0f
        );
    }

    private bool DecodeNextFrame()
    {
        while (true) {
            //Check if there's a decoded frame available before reading more packets.
            if (_decoder.ReceiveFrame(_frame)) {
                _timestamp = _stream.GetTimestamp(_frame.PresentationTimestamp!.Value);
                UploadFrame();
                return true;
            }
            if (!_demuxer.Read(_packet)) {
                return false; //end of file
            }
            if (_packet.StreamIndex == _stream.Index) {
                _decoder.SendPacket(_packet);
            }
        }
    }

    private void UploadFrame()
    {
        Debug.Assert(_frame.IsHardwareFormat); //TODO: implement support for SW frames

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
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        if (KeyboardState.IsKeyDown(Keys.Escape)) Close();
        
        if (KeyboardState.IsKeyDown(Keys.Left)) SeekRelative(-5);
        if (KeyboardState.IsKeyDown(Keys.Right)) SeekRelative(+5);
    }

    private void SeekRelative(int secs)
    {
        Console.WriteLine("Seek to " + (_timestamp + TimeSpan.FromSeconds(secs)));
        //Note that Seek() will go to some keyframe before the requested timestamp.
        //If more precision is needed, frames should be decoded and discarded until the desired timestamp.
        if (_demuxer.Seek(_timestamp + TimeSpan.FromSeconds(secs))) {
            _decoder.Flush();
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
        _frame.Dispose();
        _packet.Dispose();

        _shader.Dispose();
        _emptyVbo.Dispose();
        _textureY.Dispose();
        _textureUV.Dispose();
        _format.Dispose();
    }
}