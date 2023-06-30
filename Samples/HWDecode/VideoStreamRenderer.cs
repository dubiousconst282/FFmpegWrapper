using System.Diagnostics;

using FFmpeg.Wrapper;

using GL2O;

using OpenTK.Windowing.Desktop;

using Matrix3x3 = OpenTK.Mathematics.Matrix3;

public class VideoStreamRenderer : StreamRenderer
{
    VideoFrame _currFrame = new(), _nextFrame = new();
    bool _hasFrame = false;

    Texture2D _texY = null!, _texUV = null!;
    ShaderProgram _shader;
    BufferObject _emptyVbo;
    VertexFormat _emptyVao;

    IGLFWGraphicsContext _glfw;
    bool _flushed = false;

    public VideoStreamRenderer(MediaDemuxer demuxer, MediaStream stream, IGLFWGraphicsContext glfw)
        : base(demuxer, stream)
    {
        var decoder = (VideoDecoder)_decoder;

        //Setup HW decoder
        var hwConfig = decoder.GetHardwareConfigs().FirstOrDefault(config => config.DeviceType == HWDeviceTypes.DXVA2);
        using var device = HardwareDevice.Create(hwConfig.DeviceType);

        if (device != null) {
            decoder.SetupHardwareAccelerator(hwConfig, device);
        }
        decoder.Open();
        
        string shaderBasePath = AppContext.BaseDirectory + "shaders/";
        _shader = new ShaderProgram();
        _shader.AttachFile(ShaderType.VertexShader, shaderBasePath + "full_screen_quad.vert");
        _shader.AttachFile(ShaderType.FragmentShader, shaderBasePath + "render_yuv.frag");
        _shader.Link();

        _emptyVbo = new BufferObject(16, BufferStorageFlags.None);
        _emptyVao = VertexFormat.CreateEmpty();

        _glfw = glfw;
    }

    public override void Tick(PlayerClock refClock, ref TimeSpan tickInterval)
    {
        bool gotFrame = false;

        //Loop if we need to skip frames for synchronization
        while (true) {
            if (!_hasFrame && !ReceiveFrame(_nextFrame)) break;

            _hasFrame = true;

            var nextPts = Stream.GetTimestamp(_nextFrame.BestEffortTimestamp ?? 0);
            var currTime = Clock.GetFrameTime();

            var refTime = refClock.GetFrameTime();
            var timeLeft = nextPts - refTime;

            //Console.WriteLine($"Ref={refTime} {(currTime - refTime).TotalMilliseconds:0} C={Clock.FramePts} N={nextPts} {timeLeft.TotalMilliseconds:0.0}");

            if (timeLeft > tickInterval && !_flushed) break;

            gotFrame = true;
            _hasFrame = false;
            _flushed = false;
            Clock.SetFrameTime(nextPts);

            //Swap frames for the next iteration because ReceiveFrame() is destructive.
            (_currFrame, _nextFrame) = (_nextFrame, _currFrame);
        }

        if (gotFrame) {
            UploadFrame(_currFrame);

            _texY.BindUnit(0);
            _texUV.BindUnit(1);
            _shader.SetUniform("u_TextureY", 0);
            _shader.SetUniform("u_TextureUV", 1);
            _shader.SetUniform("u_Yuv2RgbCoeffs", GetYuv2RgbCoeffsMatrix());
            _shader.DrawArrays(PrimitiveType.Triangles, _emptyVao, _emptyVbo, 0, 3);

            _glfw.SwapBuffers();
            tickInterval = TimeSpan.Zero;
        }
    }

    public override void Flush()
    {
        base.Flush();
        _flushed = true;
    }

    private void UploadFrame(VideoFrame decodedFrame)
    {
        //There's no easy way to interop between HW and GL surfaces, so we'll have to do a copy through the CPU here.
        //For what is worth, a 4K 60FPS P010 stream will in theory only take ~1400MB/s of bandwidth, which is not that bad.

        Debug.Assert(decodedFrame.IsHardwareFrame); //TODO: implement support for SW frames

        //TODO: Use TransferTo() when Map() fails.
        using var frame = decodedFrame.Map(HardwareFrameMappingFlags.Read | HardwareFrameMappingFlags.Direct)!;

        var (pixelType, pixelStride) = frame.PixelFormat switch {
            PixelFormats.NV12 => (PixelType.UnsignedByte, 1),
            PixelFormats.P010LE => (PixelType.UnsignedShort, 2)
        };

        _texY ??= new Texture2D(frame.Width, frame.Height, 1, SizedInternalFormat.R8);
        _texUV ??= new Texture2D(frame.Width / 2, frame.Height / 2, 1, SizedInternalFormat.Rg8);

        _texY.SetPixels<byte>(
            frame.GetPlaneSpan<byte>(0, out int strideY),
            0, 0, frame.Width, frame.Height,
            PixelFormat.Red, pixelType, rowLength: strideY / pixelStride);

        _texUV.SetPixels<byte>(
            frame.GetPlaneSpan<byte>(1, out int strideUV),
            0, 0, frame.Width / 2, frame.Height / 2,
            PixelFormat.Rg, pixelType, rowLength: strideUV / pixelStride / 2);
    }

    private Matrix3x3 GetYuv2RgbCoeffsMatrix()
    {
        //TODO: consider colorspaces and stuff
        // - https://github.com/FFmpeg/FFmpeg/blob/925ac0da32697ef5853e90e0be56e106208099e2/libswscale/yuv2rgb.c
        // - https://github.com/FFmpeg/FFmpeg/blob/master/libavutil/csp.c#L46

        //https://en.wikipedia.org/wiki/YCbCr#ITU-R_BT.709_conversion
        return new Matrix3x3(
            1.0f, 0.0f, 1.5748f,
            1.0f, -0.187324f, -0.468124f,
            1.0f, 1.8556f, 0.0f
        );
    }

    public override void Dispose()
    {
        base.Dispose();

        _decoder.Dispose();

        _texY.Dispose();
        _texUV.Dispose();
        _shader.Dispose();
        _emptyVbo.Dispose();
        _emptyVao.Dispose();
    }
}