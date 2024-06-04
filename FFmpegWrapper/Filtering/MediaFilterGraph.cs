namespace FFmpeg.Wrapper;

using System.Text;

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
            ContextOption.Set(node, key, val, searchChildren: true);
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

    public MediaBufferSource AddAudioBufferSource(AudioFormat format, Rational timeBase)
    {
        var pars = ffmpeg.av_buffersrc_parameters_alloc();
        pars->format = (int)format.SampleFormat;
        pars->sample_rate = format.SampleRate;
        pars->ch_layout = format.Layout.Native;
        pars->time_base = timeBase;
        return AddBufferSource(pars, "abuffer");
    }

    /// <param name="frameRate"> The frame rate of the input video. Must only be  set to a non-zero value if input stream has a known constant framerate and should be left at its initial value if the framerate is variable or unknown.</param>
    [Obsolete("Use AddVideoBufferSource(PictureFormat format, PictureColorspace colorspace, Rational timeBase, Rational? frameRate)")]
    public MediaBufferSource AddVideoBufferSource(PictureFormat format, Rational frameRate, Rational timeBase)
    {
        var pars = ffmpeg.av_buffersrc_parameters_alloc();
        pars->width = format.Width;
        pars->height = format.Height;
        pars->format = (int)format.PixelFormat;
        pars->frame_rate = frameRate;
        pars->sample_aspect_ratio = format.PixelAspectRatio;
        pars->time_base = timeBase;
        return AddBufferSource(pars, "buffer");
    }

    /// <param name="frameRate"> 
    /// The frame rate of the input video. 
    /// Must only be set to a non-zero value if input stream has a known constant framerate and should be left at its initial value (0/0) if the framerate is variable or unknown.
    /// See also <seealso cref="AVBufferSrcParameters.frame_rate"/> <seealso cref="AVFilterLink.frame_rate"/>
    /// </param>
    public MediaBufferSource AddVideoBufferSource(PictureFormat format, PictureColorspace colorspace, Rational timeBase, Rational? frameRate)
    {
        var pars = ffmpeg.av_buffersrc_parameters_alloc();
        pars->width = format.Width;
        pars->height = format.Height;
        pars->format = (int)format.PixelFormat;
        pars->sample_aspect_ratio = format.PixelAspectRatio;
        pars->color_range = colorspace.Range;
        pars->color_space = colorspace.Matrix;
        pars->time_base = timeBase;
        if (frameRate.HasValue) {
            pars->frame_rate = frameRate.Value;
        }
        return AddBufferSource(pars, "buffer");
    }

    private MediaBufferSource AddBufferSource(AVBufferSrcParameters* pars, string filterName)
    {
        ThrowIfConfigured();

        try {
            var node = ffmpeg.avfilter_graph_alloc_filter(_ctx, ffmpeg.avfilter_get_by_name(filterName), "source");
            ffmpeg.av_buffersrc_parameters_set(node, pars).CheckError("Failed to set buffer source parameters");
            ffmpeg.avfilter_init_str(node, null).CheckError("Failed to initialize buffer source node");
            return new MediaBufferSource(node);
        } finally {
            ffmpeg.av_free(pars);
        }
    }

    public AudioBufferSink AddAudioBufferSink(MediaFilterNodePort input)
    {
        return new AudioBufferSink(AddBufferSink(input, "abuffersink"));
    }
    public VideoBufferSink AddVideoBufferSink(MediaFilterNodePort input)
    {
        return new VideoBufferSink(AddBufferSink(input, "buffersink"));
    }

    private AVFilterContext* AddBufferSink(MediaFilterNodePort input, string filterName)
    {
        ThrowIfConfigured();

        var node = ffmpeg.avfilter_graph_alloc_filter(_ctx, ffmpeg.avfilter_get_by_name(filterName), "sink");
        ffmpeg.avfilter_init_str(node, null).CheckError("Failed to initialize buffer sink node");
        ffmpeg.avfilter_link(input.Node.Handle, (uint)input.Index, node, 0).CheckError("Failed to link input node to buffer sink");
        return node;
    }

    /// <summary> Parses and initializes a graph segment described by the given string. </summary>
    /// <returns> A map of named outputs from the segment. </returns>
    public Dictionary<string, MediaFilterNodePort> Parse(string str, params (string Name, MediaFilterNodePort)[] inputs)
    {
        ThrowIfConfigured();

        AVFilterInOut* inputLinks = null;
        AVFilterInOut* outputLinks = null;

        try {
            foreach (var (name, port) in inputs) {
                var link = ffmpeg.avfilter_inout_alloc();

                link->name = ffmpeg.av_strdup(name);
                link->filter_ctx = port.Node.Handle;
                link->pad_idx = port.Index;
                link->next = inputLinks;

                inputLinks = link;
            }
            //This function names inputs/outputs pars to the caller's perspective,
            //so output[i] is actually the input of some parsed node.
            ffmpeg.avfilter_graph_parse_ptr(_ctx, str, &outputLinks, &inputLinks, null).CheckError("Failed to parse filter graph");

            if (outputLinks != null) {
                throw new InvalidOperationException("Parsed filter graph cannot have open inputs");
            }
            var outputs = new Dictionary<string, MediaFilterNodePort>();

            for (AVFilterInOut* link = inputLinks; link != null; link = link->next) {
                string name = Helpers.PtrToStringUTF8(link->name)!;
                var node = new MediaFilterNode(link->filter_ctx); //TODO: handle buffer sinks and other derived nodes
                outputs.Add(name, new MediaFilterNodePort(node, link->pad_idx));
            }
            return outputs;
        } finally {
            ffmpeg.avfilter_inout_free(&inputLinks);
            ffmpeg.avfilter_inout_free(&outputLinks);
        }
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

    public override string ToString()
    {
        if (_ctx == null) {
            return base.ToString();
        }

        var sb = new StringBuilder();
        for (int i = 0; i < _ctx->nb_filters; i++) {
            if (i != 0) sb.Append(",");

            var node = _ctx->filters[i];

            //Input ports
            for (int j = 0; j < node->nb_inputs; j++) {
                AVFilterLink* link = node->inputs[j];

                int srcNodeIdx = 0;
                int srcPortIdx = (int)(link->srcpad - link->src->output_pads);

                while (srcNodeIdx < _ctx->nb_filters && _ctx->filters[srcNodeIdx] != link->src) {
                    srcNodeIdx++;
                }
                PrintPort(srcNodeIdx, srcPortIdx);
            }
            //Filter name
            sb.Append(Helpers.PtrToStringUTF8(node->filter->name));
            if (node->name != null) {
                sb.Append($"@{Helpers.PtrToStringUTF8(node->name)}");
            }

            //Options
            var displayOpts = ContextOption.GetOptions(node->priv, removeAliases: true, skipDefaults: true);

            for (int j = 0; j < displayOpts.Count; j++) {
                var opt = displayOpts[j];

                sb.Append(j == 0 ? '=' : ':');
                sb.Append(opt.Name).Append("=");
                sb.Append(ContextOption.GetAsString(node->priv, opt.Name, searchChildren: false));
            }

            //Outputs
            for (int j = 0; j < node->nb_outputs; j++) {
                PrintPort(i, j);
            }
        }
        return sb.ToString();

        void PrintPort(int nodeIdx, int portIdx)
        {
            sb.Append("[");
            do {
                sb.Append((char)('A' + (nodeIdx % 26)));
                nodeIdx /= 26;
            } while (nodeIdx != 0);

            sb.Append(portIdx + "]");
        }
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
    public List<MediaFilterNodePort> Inputs { get; set; } = new();

    /// <summary> List of arguments used to initialize the filter. </summary>
    public List<(string Key, OptionValue Value)> Arguments { get; set; } = new();
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

    public MediaFilterNodePort GetOutput(int index)
    {
        if (index < 0 || index >= Handle->nb_outputs) {
            throw new ArgumentOutOfRangeException();
        }
        return new MediaFilterNodePort(this, index);
    }

    internal MediaFilterNode(AVFilterContext* handle) => Handle = handle;
}
public readonly struct MediaFilterNodePort
{
    public MediaFilterNode Node { get; }
    public int Index { get; }
    
    public unsafe AVMediaType Type => ffmpeg.avfilter_pad_get_type(Node.Handle->output_pads, Index);

    /// <summary> Whether this port has been connected to a filter input. </summary>
    public unsafe bool IsConnected => Node.Handle->outputs[Index] != null;

    public MediaFilterNodePort(MediaFilterNode node, int index)
    {
        Node = node;
        Index = index;
    }
}