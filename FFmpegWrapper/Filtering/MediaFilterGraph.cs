namespace FFmpeg.Wrapper;

using System.Globalization;

public unsafe class MediaFilterGraph : FFObject
{
    protected AVFilterGraph* _ctx;

    public AVFilterGraph* Handle {
        get {
            ThrowIfDisposed();
            return _ctx;
        }
    }
    public bool IsConfigured { get; private set; }

    public MediaFilterGraph()
    {
        _ctx = ffmpeg.avfilter_graph_alloc();

        if (_ctx == null) {
            throw new OutOfMemoryException();
        }
    }

    public MediaFilterNode AddNode(MediaFilterArgs args)
    {
        ThrowIfConfigured();

        var node = ffmpeg.avfilter_graph_alloc_filter(_ctx, args.Filter.Handle, args.NodeName);
        if (node == null) {
            throw new OutOfMemoryException();
        }

        foreach (var (key, val) in args.Arguments) {
            int res = SetBoxedOption(node, key, val, ffmpeg.AV_OPT_SEARCH_CHILDREN);
            if (res < 0) {
                res.ThrowError($"Invalid option '{key}' for filter '{args.Filter.Name}'");
            }
        }
        if (args.HardwareDevice != null) {
            node->hw_device_ctx = ffmpeg.av_buffer_ref(args.HardwareDevice.Handle);
        }
        ffmpeg.avfilter_init_str(node, null).CheckError("Failed to initialize filter node");

        if (args.Inputs.Count != node->nb_inputs) {
            throw new ArgumentException("Invalid number of inputs for filter node " + args.NodeName ?? args.Filter.Name);
        }

        uint i = 0;
        foreach (var pad in args.Inputs) {
            ffmpeg.avfilter_link(pad.Node.Handle, (uint)pad.Index, node, i++).CheckError("Failed to link filter node pads");
        }

        return new MediaFilterNode(node);
    }

    private static int SetBoxedOption(AVFilterContext* node, string key, object val, int searchFlags)
    {
        var type = Type.GetTypeCode(val.GetType());

        if (type is (>= TypeCode.SByte and <= TypeCode.Double) or TypeCode.Boolean or TypeCode.String) {
            return ffmpeg.av_opt_set(node, key, string.Format(CultureInfo.InvariantCulture, "{0}", val), searchFlags);
        }
        if (val is Rational qw) {
            return ffmpeg.av_opt_set_q(node, key, qw, searchFlags);
        }
        if (val is AVRational q) {
            return ffmpeg.av_opt_set_q(node, key, q, searchFlags);
        }
        if (val is AVChannelLayout chl) {
            return ffmpeg.av_opt_set_chlayout(node, key, &chl, searchFlags);
        }
        return ffmpeg.AVERROR_INVALIDDATA;
    }

    public MediaBufferSource AddAudioBufferSource(AudioFormat format, Rational timeBase)
    {
        var pars = new AVBufferSrcParameters() {
            format = (int)format.SampleFormat,
            sample_rate = format.SampleRate,
            ch_layout = format.Layout,
            time_base = timeBase
        };
        return AddBufferSource(&pars, "abuffer");
    }

    /// <param name="frameRate"> The frame rate of the input video. Must only be  set to a non-zero value if input stream has a known constant framerate and should be left at its initial value if the framerate is variable or unknown.</param>
    public MediaBufferSource AddVideoBufferSource(PictureFormat format, Rational frameRate, Rational timeBase)
    {
        var pars = new AVBufferSrcParameters() {
            width = format.Width,
            height = format.Height,
            format = (int)format.PixelFormat,
            frame_rate = frameRate,
            sample_aspect_ratio = format.PixelAspectRatio,
            time_base = timeBase
        };
        return AddBufferSource(&pars, "buffer");
    }

    private MediaBufferSource AddBufferSource(AVBufferSrcParameters* pars, string filterName)
    {
        ThrowIfConfigured();

        var node = ffmpeg.avfilter_graph_alloc_filter(_ctx, ffmpeg.avfilter_get_by_name(filterName), "source");
        ffmpeg.av_buffersrc_parameters_set(node, pars).CheckError("Failed to set buffer source parameters");
        ffmpeg.avfilter_init_str(node, null).CheckError("Failed to initialize buffer source node");
        return new MediaBufferSource(node);
    }

    public AudioBufferSink AddAudioBufferSink(MediaFilterNodePad input)
    {
        return new AudioBufferSink(AddBufferSink(input, "abuffersink"));
    }
    public VideoBufferSink AddVideoBufferSink(MediaFilterNodePad input)
    {
        return new VideoBufferSink(AddBufferSink(input, "buffersink"));
    }

    private AVFilterContext* AddBufferSink(MediaFilterNodePad input, string filterName)
    {
        ThrowIfConfigured();

        var node = ffmpeg.avfilter_graph_alloc_filter(_ctx, ffmpeg.avfilter_get_by_name(filterName), "sink");
        ffmpeg.avfilter_init_str(node, null).CheckError("Failed to initialize buffer sink node");
        ffmpeg.avfilter_link(input.Node.Handle, (uint)input.Index, node, 0).CheckError("Failed to link input node to buffer sink");
        return node;
    }

    public void SetOption(string name, string value)
    {
        ThrowIfConfigured();
        ffmpeg.av_opt_set(Handle, name, value, 0).CheckError();
    }

    public void Configure()
    {
        ThrowIfConfigured();

        ffmpeg.avfilter_graph_config(_ctx, null).CheckError();
        IsConfigured = true;
    }

    protected override void Free()
    {
        fixed (AVFilterGraph** c = &_ctx) {
            ffmpeg.avfilter_graph_free(c);
        }
    }
    protected void ThrowIfDisposed()
    {
        if (_ctx == null) {
            throw new ObjectDisposedException(nameof(MediaFilterGraph));
        }
    }
    protected void ThrowIfConfigured()
    {
        ThrowIfDisposed();

        if (IsConfigured) {
            throw new InvalidOperationException("Value must be set before the filter graph is configured.");
        }
    }
}

public class MediaFilterArgs
{
    public MediaFilter Filter { get; set; }
    public List<MediaFilterNodePad> Inputs { get; set; } = new();

    /// <summary> List of arguments used to initialize the filter. Values must be number/string/bool/Rational/AVChannelLayout. </summary>
    public List<(string Key, object Value)> Arguments { get; set; } = new();
    public string? NodeName { get; set; }

    public HardwareDevice? HardwareDevice { get; set; }

    public string FilterName {
        set => Filter = MediaFilter.Get(value);
    }
}

public unsafe class MediaFilterNode
{
    public AVFilterContext* Handle { get; }

    public string? Name => Helpers.PtrToStringUTF8(Handle->name);
    public MediaFilter Filter => new(Handle->filter);

    public MediaFilterNodePad GetOutput(int index)
    {
        if (index < 0 || index >= Handle->nb_outputs) {
            throw new ArgumentOutOfRangeException();
        }
        return new MediaFilterNodePad(this, index);
    }

    internal MediaFilterNode(AVFilterContext* handle) => Handle = handle;
}
public readonly struct MediaFilterNodePad
{
    public MediaFilterNode Node { get; }
    public int Index { get; }

    public MediaFilterNodePad(MediaFilterNode node, int index)
    {
        Node = node;
        Index = index;
    }
}