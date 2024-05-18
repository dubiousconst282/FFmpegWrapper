namespace FFmpeg.Wrapper;

/// <summary> Convenience wrapper for a video filter graph with one input and output. </summary>
public unsafe class VideoFilterPipeline : IDisposable
{
    private readonly MediaFilterGraph _graph;
    private readonly MediaBufferSource? _source;
    private readonly VideoBufferSink _sink;

    public PictureFormat Format => _sink.Format;
    public Rational TimeBase => _sink.TimeBase;
    public Rational FrameRate => _sink.FrameRate;

    private VideoFilterPipeline(MediaFilterGraph graph, MediaBufferSource? source, VideoBufferSink sink)
    {
        if (!graph.IsConfigured) {
            graph.Configure();
        }
        _graph = graph;
        _source = source;
        _sink = sink;
    }

    /// <summary> Creates a filter pipeline from a graph string that has one buffered source. </summary>
    public static VideoFilterPipeline CreateBufferedFromString(string graphString, PictureFormat inputFormat, PictureColorspace colorspace, Rational timeBase, Rational? frameRate)
    {
        var graph = new MediaFilterGraph();
        var source = graph.AddVideoBufferSource(inputFormat, colorspace, timeBase, frameRate);
        var parsedSegment = graph.Parse(graphString, ("in", source.GetOutput(0)));
        var sink = graph.AddVideoBufferSink(parsedSegment["out"]);
        return new VideoFilterPipeline(graph, source, sink);
    }

    /// <summary> Creates a filter pipeline from a graph string that have no inputs. </summary>
    public static VideoFilterPipeline CreateSourceFromString(string graphString)
    {
        var graph = new MediaFilterGraph();
        var pipeline = graph.Parse(graphString);
        var sink = graph.AddVideoBufferSink(pipeline["out"]);
        return new VideoFilterPipeline(graph, null, sink);
    }

    /// <summary> Apply filters to the given frame frame. </summary>
    /// <remarks> This assumes that the filter pipeline outputs exactly one frame per input. The the Send/Receive APIs must be used otherwise. </remarks> 
    public VideoFrame Apply(VideoFrame frame)
    {
        if (_source == null) {
            throw new InvalidOperationException("In-place filtering can only be used for buffered filter pipelines.");
        }
        _source!.SendFrame(frame);
        return _sink.ReceiveFrame() ?? throw new NotSupportedException();
    }

    public void SendFrame(VideoFrame frame)
    {
        if (_source == null) {
            throw new InvalidOperationException("Filter pipeline does not accept any input frames.");
        }
        _source.SendFrame(frame);
    }
    public VideoFrame? ReceiveFrame() => _sink.ReceiveFrame();

    public void Dispose()
    {
        _graph.Dispose();
    }

    // Helper factories

    /// <summary> Creates a filter that auto-rotates the input based on the metadata display matrix. </summary>
    public static VideoFilterPipeline? CreateAutoRotate(MediaStream stream)
    {
        var displayMatrix = stream.CodecPars.GetSideData<int_array9>(AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX);
        if (displayMatrix == null) return null;

        string? desc = GetAutoRotateFilterString(displayMatrix->ToArray());
        return desc == null ? null : CreateBufferedFromString(desc, stream.CodecPars.PictureFormat, stream.CodecPars.Colorspace, stream.TimeBase, null);
    }
    public static string? GetAutoRotateFilterString(int[] displayMatrix)
    {
        // https://github.com/FFmpeg/FFmpeg/blob/7b47099bc080ee597327476c0df44d527c349862/fftools/ffmpeg_filter.c#L1711
        double angle = ffmpeg.av_display_rotation_get(in Unsafe.As<int, int_array9>(ref displayMatrix[0])); // counterclockwise. in range [-180.0, 180.0]
        if (double.IsNaN(angle)) return null;

        // https://github.com/FFmpeg/FFmpeg/blob/cdcb4b98b7f74d87a6274899ff70724795d551cb/fftools/cmdutils.c#L1107 
        angle = -Math.Round(angle); // clockwise
        angle -= 360 * Math.Floor((angle / 360) + (0.9 / 360)); // clamp to [0, 360)

        int theta = (int)angle;
        string desc = string.Empty;

        if (theta == 90) {
            desc = displayMatrix[3] > 0 ? "transpose=cclock_flip" : "transpose=clock";
        } else if (theta == 180) {
            if (displayMatrix[0] < 0) {
                desc += "hflip";
            }
            if (displayMatrix[4] < 0) {
                desc += desc.Length > 0 ? ",vflip" : "vflip";
            }
        } else if (theta == 270) {
            desc = displayMatrix[3] < 0 ? "transpose=clock_flip" : "transpose=cclock";
        } else if (theta == 0) {
            if (displayMatrix[4] < 0) {
                desc = "vflip";
            }
        }
        return desc.Length == 0 ? null : desc;
    }
}