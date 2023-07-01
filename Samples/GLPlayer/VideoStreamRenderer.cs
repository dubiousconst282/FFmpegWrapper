using System.Diagnostics;

using FFmpeg.Wrapper;

using GL2O;

using OpenTK.Windowing.Desktop;

public class VideoStreamRenderer : StreamRenderer
{
    VideoFrame _currFrame = new(), _nextFrame = new();
    bool _hasFrame = false;
    bool _isHDR = false;

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

        //https://en.wikipedia.org/wiki/Perceptual_quantizer
        _isHDR = stream.CodecPars.ColorTrc == FFmpeg.AutoGen.AVColorTransferCharacteristic.AVCOL_TRC_SMPTE2084;

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
            _shader.SetUniform("u_ConvertHDRtoSDR", _isHDR ? 1 : 0);
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

        bool highDepth = pixelStride == 2;

        _texY ??= new Texture2D(frame.Width, frame.Height, 1, highDepth ? SizedInternalFormat.R16 : SizedInternalFormat.R8);
        _texUV ??= new Texture2D(frame.Width / 2, frame.Height / 2, 1, highDepth ? SizedInternalFormat.Rg16 : SizedInternalFormat.Rg8);

        _texY.SetPixels<byte>(
            frame.GetPlaneSpan<byte>(0, out int strideY),
            0, 0, frame.Width, frame.Height,
            PixelFormat.Red, pixelType, rowLength: strideY / pixelStride);

        _texUV.SetPixels<byte>(
            frame.GetPlaneSpan<byte>(1, out int strideUV),
            0, 0, frame.Width / 2, frame.Height / 2,
            PixelFormat.Rg, pixelType, rowLength: strideUV / pixelStride / 2);
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