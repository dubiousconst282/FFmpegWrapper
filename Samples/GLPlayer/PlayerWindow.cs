using FFmpeg.Wrapper;

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

//Mini OpenGL video player using hardware accelerated decoding.
//Features:
// - Audio playback via the Windows Audio Session API 
// - Crude A/V synchronization (video stream follows audio).
// - HDR to SDR output (SMPTE 2084)
public class PlayerWindow : NativeWindow
{
    private MediaDemuxer _demuxer;
    private VideoStreamRenderer _videoStream;
    private AudioStreamRenderer _audioStream;
    private MediaPacket? _packet;

    private PlayerClock _refClock => _audioStream.Clock;

    public PlayerWindow(string videoPath)
        : base(
            new NativeWindowSettings() {
                Flags = ContextFlags.Debug | ContextFlags.ForwardCompatible,
                APIVersion = new Version(4, 5),
                Vsync = VSyncMode.On,
                Size = Monitors.GetPrimaryMonitor().WorkArea.Size * 8 / 10
            })
    {
        _demuxer = new MediaDemuxer(videoPath);

        var videoStream = _demuxer.FindBestStream(MediaTypes.Video);
        _videoStream = new VideoStreamRenderer(_demuxer, videoStream!, Context);

        var audioStream = _demuxer.FindBestStream(MediaTypes.Audio);
        _audioStream = new AudioStreamRenderer(_demuxer, audioStream!);
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
                    var renderer =
                        _packet.StreamIndex == _videoStream.Stream.Index ? _videoStream :
                        _packet.StreamIndex == _audioStream.Stream.Index ? _audioStream : null as StreamRenderer;
                        
                    if (renderer != null && !renderer.EnqueuePacket(_packet)) break; //Queue is full

                    _packet = null;
                }

                _packet ??= new MediaPacket();
                if (!_demuxer.Read(_packet)) break; //End of Input
            }

            OnResize(new ResizeEventArgs(ClientSize));

            var interval = TimeSpan.FromMilliseconds(Context.SwapInterval);

            _videoStream.Tick(_refClock, ref interval);
            _audioStream.Tick(_refClock, ref interval);

            var vtime = _videoStream.Clock.GetFrameTime();
            var atime = _audioStream.Clock.GetFrameTime();
            Title = $"{_refClock.GetFrameTime()} A-V: {(vtime - atime).TotalMilliseconds:0}";

            Thread.Sleep(interval);
        }
        OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        if (_videoStream == null) return;
        var pars = _videoStream.Stream.CodecPars;
        
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
        var newTime = _refClock.GetFrameTime() + TimeSpan.FromSeconds(secs);
        Console.WriteLine("Seek to " + newTime);

        var opts = secs < 0 ? SeekOptions.Backward : SeekOptions.Forward;

        if (_demuxer.Seek(newTime, opts)) {
            _videoStream.Flush();
            _audioStream.Flush();
            _packet = null;
        }
    }

    private void OnUnload()
    {
        _videoStream.Dispose();
        _audioStream.Dispose();
        _demuxer.Dispose();
    }
}