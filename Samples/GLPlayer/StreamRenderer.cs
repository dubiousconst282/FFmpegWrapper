using System.Diagnostics;

using FFmpeg.Wrapper;

public abstract class StreamRenderer : IDisposable
{
    public MediaStream Stream { get; }
    protected MediaDecoder _decoder;

    private Queue<MediaPacket> _packetQueue = new();
    public PlayerClock Clock { get; } = new();

    public StreamRenderer(MediaDemuxer demuxer, MediaStream stream)
    {
        Stream = stream;
        _decoder = demuxer.CreateStreamDecoder(stream, open: false);
    }
    
    public bool EnqueuePacket(MediaPacket packet)
    {
        if (_packetQueue.Count < 128) {
            _packetQueue.Enqueue(packet);
            return true;
        }
        return false;
    }

    protected bool ReceiveFrame(MediaFrame frame)
    {
        while (true) {
            if (_decoder.ReceiveFrame(frame)) {
                return true;
            }
            if (_packetQueue.TryDequeue(out var packet)) {
                _decoder.SendPacket(packet);
                packet.Dispose();
            } else {
                return false;
            }
        }
    }

    public abstract void Tick(PlayerClock refClock, ref TimeSpan tickInterval);

    /// <summary> Flush decoder and buffered frames. </summary>
    public virtual void Flush()
    {
        _decoder.Flush();
        _packetQueue.Clear();
    }

    public virtual void Dispose()
    {
        _decoder.Dispose();
    }
}
public class PlayerClock
{
    public TimeSpan FramePts;         //PTS of the current frame
    public TimeSpan FrameDisplayTime; //Clock time at which the current frame was displayed

    public TimeSpan GetFrameTime()
    {
        return FramePts + (GetCurrentTime() - FrameDisplayTime);
    }
    public void SetFrameTime(TimeSpan pts)
    {
        FramePts = pts;
        FrameDisplayTime = GetCurrentTime();
    }

    static readonly long s_GlobalStartTime = Stopwatch.GetTimestamp();
    public static TimeSpan GetCurrentTime() => Stopwatch.GetElapsedTime(s_GlobalStartTime);

}