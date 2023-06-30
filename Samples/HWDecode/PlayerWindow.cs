using FFmpeg.Wrapper;

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

//Crude OpenGL video player using hardware accelerated decoding.
//This supports audio playback via the Windows Audio Session API, but there's no
//A/V synchronization or compensation, so they may drift or be badly synced in general.
public class PlayerWindow : NativeWindow
{
    private MediaDemuxer _demuxer;
    private List<StreamRenderer> _streams = new();
    private MediaPacket? _packet;

    private PlayerClock RefClock => _streams[^1].Clock;

    public PlayerWindow(string videoPath)
        : base(
            new NativeWindowSettings() {
                Size = new(1280, 720), //use small window because fullscreen gets annoying during dev
                Flags = ContextFlags.Debug | ContextFlags.ForwardCompatible,
                APIVersion = new Version(4, 5),
                Vsync = VSyncMode.On
            })
    {
        _demuxer = new MediaDemuxer(videoPath);

        var videoStream = _demuxer.FindBestStream(MediaTypes.Video);
        if (videoStream != null) {
            _streams.Add(new VideoStreamRenderer(_demuxer, videoStream, Context));
        }

        var audioStream = _demuxer.FindBestStream(MediaTypes.Audio);
        if (audioStream != null) {
            _streams.Add(new AudioStreamRenderer(_demuxer, audioStream));
        }
    }

    public unsafe void Run()
    {
        Context.MakeCurrent();

        while (!GLFW.WindowShouldClose(WindowPtr)) {
            ProcessInputEvents();
            ProcessWindowEvents(waitForEvents: false);

            HandleInputs();

            //Read input and fill-up packet queues
            while (true) {
                if (_packet != null) {
                    var renderer = _streams.Find(r => r.Stream.Index == _packet.StreamIndex);
                    if (renderer != null && !renderer.EnqueuePacket(_packet)) break; //Queue is full

                    _packet = null;
                }

                _packet ??= new MediaPacket();
                if (!_demuxer.Read(_packet)) break; //End of Input
            }

            OnResize(new ResizeEventArgs(ClientSize));

            var refClock = _streams[^1].Clock;
            var interval = TimeSpan.FromMilliseconds(Context.SwapInterval);
            
            foreach (var stream in _streams) {
                stream.Tick(refClock, ref interval);
            }

            var vst = _streams[0];
            var ast = _streams[1];

            Title = $"{RefClock.GetFrameTime()} A-V: {(vst.Clock.GetFrameTime() - ast.Clock.GetFrameTime()).TotalMilliseconds:0} {interval}";

            Thread.Sleep(interval);
        }
        OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        var renderer = _streams.FirstOrDefault(r => r.Stream.Type == MediaTypes.Video)!;
        var pars = renderer.Stream.CodecPars;
        
        double scale = Math.Min(e.Width / (double)pars.Width, e.Height / (double)pars.Height);
        int w = (int)Math.Round(pars.Width * scale);
        int h = (int)Math.Round(pars.Height * scale);
        int x = (e.Width - w) / 2;
        int y = (e.Height - h) / 2;

        //FIXME: flickering after resize (caused by double buffering)
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.Viewport(x, y, w, h);
    }

    private void HandleInputs()
    {
        if (IsKeyPressed(Keys.Escape)) Close();
        
        if (IsKeyPressed(Keys.Left)) SeekRelative(-10);
        if (IsKeyPressed(Keys.Right)) SeekRelative(+10);
    }

    private void SeekRelative(int secs)
    {
        var newTime = RefClock.GetFrameTime() + TimeSpan.FromSeconds(secs);
        Console.WriteLine("Seek to " + newTime);

        var opts = secs < 0 ? SeekOptions.Backward : SeekOptions.Forward;

        if (_demuxer.Seek(newTime, opts)) {
            foreach (var stream in _streams) {
                stream.Flush();
            }
            _packet = null;
        }
    }

    private void OnUnload()
    {
        foreach (var stream in _streams) {
            stream.Dispose();
        }
        _demuxer.Dispose();
    }
}