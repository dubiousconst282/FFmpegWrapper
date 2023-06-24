using FFmpeg.Wrapper;

using GL2O;

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;

using Matrix3x3 = OpenTK.Mathematics.Matrix3;

//Crude OpenGL video player using hardware accelerated decoding.
//This supports audio playback via the Windows Audio Session API, but there's no
//A/V synchronization or compensation, so they may drift or be badly synced in general.
public unsafe class PlayerWindow : GameWindow
{
    private MediaDemuxer _demuxer;
    private MediaPacket _packet = new();

    private MediaStream _stream;
    private VideoDecoder _decoder;
    private VideoFrame _frame = new();

    private MediaStream _audioStream;
    private AudioDecoder _audioDecoder;
    private AudioFrame _audioFrame = new();
    private SwResampler _resampler;
    private IAudioSink _audioSink;

    private ShaderProgram _shader = null!;
    private Texture2D _textureY = null!, _textureUV = null!;
    private VertexFormat _format = null!;
    private BufferObject _emptyVbo = null!;

    private double _avgDecodeTime;
    private TimeSpan _timestamp;
    private TimeSpan _audioTimestamp;

    public PlayerWindow(string videoPath)
        : base(
            new GameWindowSettings(),
            new NativeWindowSettings() {
                Size = new(1280, 720),
                Flags = ContextFlags.Debug | ContextFlags.ForwardCompatible,
                APIVersion = new Version(4, 5)
            })
    {
        _demuxer = new MediaDemuxer(videoPath);

        //Setup video decoder
        _stream = _demuxer.FindBestStream(MediaTypes.Video)!;
        _decoder = (VideoDecoder)_demuxer.CreateStreamDecoder(_stream, open: false);

        //VAAPI has the best support in ffmpeg but it's Linux only.
        //D3D11VA doesn't support frame mappings used in this example, so that leaves us with DXVA2.
        var hwConfig = _decoder.GetHardwareConfigs().FirstOrDefault(config => config.DeviceType == HWDeviceTypes.DXVA2);

        if (hwConfig.DeviceType != HWDeviceTypes.None) {
            //Note that HardwareDevice is ref-counted, it's safe to dispose
            //of it here as the decoder will increment the counter.
            using var device = HardwareDevice.Create(hwConfig.DeviceType);

            if (device != null) {
                _decoder.SetupHardwareAccelerator(hwConfig, device);
            }
        }
        _decoder.Open();

        //Setup audio decoder and playback engine
        _audioStream = _demuxer.FindBestStream(MediaTypes.Audio)!;
        _audioDecoder = (AudioDecoder)_demuxer.CreateStreamDecoder(_audioStream, open: true);

        _audioSink = new WasapiAudioSink(_audioDecoder.Format, latencyMs: 100);
        _resampler = new SwResampler(_audioDecoder.Format, _audioSink.Format);
        _audioSink.Start();

        RenderFrequency = (double)_stream.AvgFrameRate;
        UpdateFrequency = 50;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

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

        double audioDelayMs = (_timestamp - _audioTimestamp).TotalSeconds;
        audioDelayMs += _resampler.BufferedSamples / (double)_resampler.OutputFormat.SampleRate;

        Title = $"Playing {_timestamp:mm\\:ss\\.ff} - DecodeTime: {_avgDecodeTime:0.00}ms | AudioDelay: {audioDelayMs * 1000.0:0.0}ms";

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
                _timestamp = _stream.GetTimestamp(_frame.BestEffortTimestamp!.Value);
                UploadFrame();
                return true;
            }
            //Buffer up audio samples in the resampler
            if (_audioDecoder.ReceiveFrame(_audioFrame)) {
                _audioTimestamp = _audioStream.GetTimestamp(_audioFrame.BestEffortTimestamp!.Value);
                _resampler.SendFrame(_audioFrame);
            }

            if (!_demuxer.Read(_packet)) {
                return false; //end of file
            }

            if (_packet.StreamIndex == _stream.Index) {
                _decoder.SendPacket(_packet);
            } else if (_packet.StreamIndex == _audioStream.Index) {
                _audioDecoder.SendPacket(_packet);
            }
        }
    }

    private void UploadFrame()
    {
        //There's no easy way to interop between HW and GL surfaces, so we'll have to do a copy through the CPU here.
        //For what is worth, a 4K 60FPS P010 stream will in theory only take ~1400MB/s of bandwidth. Not bad.

        Debug.Assert(_frame.IsHardwareFrame); //TODO: implement support for SW frames

        //Map() may return null if the device doesn't support frame mappings. In this case, TransferTo() should be used instead.
        using var frame = _frame.Map(HardwareFrameMappingFlags.Read | HardwareFrameMappingFlags.Direct)!;

        var (pixelType, pixelStride) = frame.PixelFormat switch {
            PixelFormats.NV12   => (PixelType.UnsignedByte, 1),
            PixelFormats.P010LE => (PixelType.UnsignedShort, 2)
        };

        _textureY.SetPixels<byte>(
            frame.GetPlaneSpan<byte>(0, out int strideY),
            0, 0, _frame.Width, _frame.Height,
            PixelFormat.Red, pixelType, rowLength: strideY / pixelStride);

        _textureUV.SetPixels<byte>(
            frame.GetPlaneSpan<byte>(1, out int strideUV),
            0, 0, _frame.Width / 2, _frame.Height / 2,
            PixelFormat.Rg, pixelType, rowLength: strideUV / pixelStride / 2);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        //Fill the play queue with resampled audio.
        var playBuffer = _audioSink.GetQueueBuffer<byte>();
        if (playBuffer.Length > 0) {
            int samplesWritten = _resampler.ReceiveFrame(playBuffer);
            _audioSink.AdvanceQueue(samplesWritten);
        }

        if (IsKeyPressed(Keys.Escape)) Close();
        
        if (IsKeyPressed(Keys.Left)) SeekRelative(-10);
        if (IsKeyPressed(Keys.Right)) SeekRelative(+10);
    }

    private void SeekRelative(int secs)
    {
        Console.WriteLine("Seek to " + (_timestamp + TimeSpan.FromSeconds(secs)));

        var opts = secs < 0 ? SeekOptions.Backward : SeekOptions.Forward;

        if (_demuxer.Seek(_timestamp + TimeSpan.FromSeconds(secs), opts)) {
            _decoder.Flush();
            _audioDecoder.Flush();
            _resampler.DropOutputSamples(_resampler.BufferedSamples * 2);
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

        _audioSink.Stop();
        _audioSink.Dispose();
        _resampler.Dispose();
        _audioDecoder.Dispose();
        _audioFrame.Dispose();

        _shader.Dispose();
        _emptyVbo.Dispose();
        _textureY.Dispose();
        _textureUV.Dispose();
        _format.Dispose();
    }
}