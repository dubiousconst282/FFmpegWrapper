namespace FFmpeg.Wrapper;

/// <summary> Convenience wrapper for a filter graph. </summary>
public partial class MediaFilterPipeline : IDisposable
{
    private readonly MediaFilterGraph _graph = new();
    private readonly Dictionary<string, MediaBufferSource> _sources = new();
    private readonly Dictionary<string, MediaBufferSink> _sinks = new();

    public static Builder CreateBuilder() => new();

    /// <summary> Apply filters to the given frame frame from the default input and output ports. </summary>
    /// <remarks> This assumes that the filter pipeline outputs exactly one frame per input. The the Send/Receive APIs must be used otherwise. </remarks> 
    /// <param name="dest"> Filter output destination. If null, will overwrite <paramref name="source"/>. </param>
    public void Apply(MediaFrame source, MediaFrame? dest = null)
    {
        SendFrame(source);

        if (!ReceiveFrame(dest ?? source)) {
            throw new NotSupportedException();
        }
    }

    public void SendFrame(MediaFrame frame, string portName = "in")
    {
        var source = GetSource(portName) ?? throw new InvalidOperationException($"Unknown filter source '{portName}'.");
        source.SendFrame(frame);
    }
    
    public bool ReceiveFrame(MediaFrame frame, string portName = "out")
    {
        var sink = GetSink(portName) ?? throw new KeyNotFoundException($"Unknown filter sink '{portName}'");
        return sink.ReceiveFrame(frame);
    }

    public void Dispose()
    {
        _graph.Dispose();
    }

    public MediaBufferSource? GetSource(string portName)
    {
        _sources.TryGetValue(portName, out var source);
        return source;
    }
    public MediaBufferSink? GetSink(string portName)
    {
        _sinks.TryGetValue(portName, out var sink);
        return sink;
    }

    public VideoBufferSink? GetVideoSink(string portName) => (VideoBufferSink?)GetSink(portName);
    public AudioBufferSink? GetAudioSink(string portName) => (AudioBufferSink?)GetSink(portName);
}