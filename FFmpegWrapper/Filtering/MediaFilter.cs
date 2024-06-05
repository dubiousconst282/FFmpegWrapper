namespace FFmpeg.Wrapper;

using System.Collections.Generic;

public unsafe readonly struct MediaFilter
{
    public AVFilter* Handle { get; }

    public string Name => Helpers.PtrToStringUTF8(Handle->name)!;
    public string? Description => Helpers.PtrToStringUTF8(Handle->description)!;
    public MediaFilterFlags Flags => (MediaFilterFlags)Handle->flags;

    public int NumInputs => (int)ffmpeg.avfilter_filter_pad_count(Handle, 0);
    public int NumOutputs => (int)ffmpeg.avfilter_filter_pad_count(Handle, 1);

    public MediaFilter(AVFilter* handle) => Handle = handle;

    public static MediaFilter Get(string name)
    {
        var ptr = ffmpeg.avfilter_get_by_name(name);
        if (ptr == null) {
            throw new KeyNotFoundException("Unknown filter '" + name + "'");
        }
        return new MediaFilter(ptr);
    }

    /// <summary> Returns a list of parameters accepted during initialization of an instance of this filter. </summary>
    public IReadOnlyList<ContextOption> GetOptions(bool removeAliases = true)
        => ContextOption.GetOptions(&Handle->priv_class, removeAliases);

    public static IEnumerable<MediaFilter> GetRegisteredFilters()
    {
        void* iter;
        AVFilter* filter;
        var list = new List<MediaFilter>(768);

        while ((filter = ffmpeg.av_filter_iterate(&iter)) != null) {
            list.Add(new MediaFilter(filter));
        }
        return list;
    }

    public override string ToString() => Name;
}

[Flags]
public enum MediaFilterFlags
{
    /// <summary>
    /// The number of the filter inputs is not determined just by AVFilter.inputs.
    /// The filter might add additional inputs during initialization depending on the
    /// options supplied to it.
    /// </summary>
    DynamicInputs = ffmpeg.AVFILTER_FLAG_DYNAMIC_INPUTS,

    /// <summary>
    /// The number of the filter outputs is not determined just by AVFilter.outputs.
    /// The filter might add additional outputs during initialization depending on
    /// the options supplied to it.
    /// </summary>
    DynamicOutputs = ffmpeg.AVFILTER_FLAG_DYNAMIC_OUTPUTS,

    /// <summary>
    /// The filter supports multithreading by splitting frames into multiple parts
    /// and processing them concurrently.
    /// </summary>
    SliceThreads = ffmpeg.AVFILTER_FLAG_SLICE_THREADS,

    /// <summary>
    /// The filter is a "metadata" filter - it does not modify the frame data in any
    /// way. It may only affect the metadata (i.e. those fields copied by
    /// av_frame_copy_props()).
    /// <para/>
    /// More precisely, this means: <br/>
    /// - video: the data of any frame output by the filter must be exactly equal to
    ///   some frame that is received on one of its inputs. Furthermore, all frames
    ///   produced on a given output must correspond to frames received on the same
    ///   input and their order must be unchanged. Note that the filter may still
    ///   drop or duplicate the frames. <br/>
    /// - audio: the data produced by the filter on any of its outputs (viewed e.g.
    ///   as an array of interleaved samples) must be exactly equal to the data
    ///   received by the filter on one of its inputs.
    /// </summary>
    MetadataOnly = ffmpeg.AVFILTER_FLAG_METADATA_ONLY,

    /// <summary>
    /// The filter can create hardware frames using AVFilterContext.hw_device_ctx.
    /// </summary>
    HardwareDevice = ffmpeg.AVFILTER_FLAG_HWDEVICE,

    /// <summary>
    /// Some filters support a generic "enable" expression option that can be used
    /// to enable or disable a filter in the timeline. Filters supporting this
    /// option have this flag set. When the enable expression is false, the default
    /// no-op filter_frame() function is called in place of the filter_frame()
    /// callback defined on each input pad, thus the frame is passed unchanged to
    /// the next filters.
    /// </summary>
    SupportTimelineGeneric = ffmpeg.AVFILTER_FLAG_SUPPORT_TIMELINE_GENERIC,

    /// <summary>
    /// Same as AVFILTER_FLAG_SUPPORT_TIMELINE_GENERIC, except that the filter will
    /// have its filter_frame() callback(s) called as usual even when the enable
    /// expression is false. The filter will disable filtering within the
    /// filter_frame() callback(s) itself, for example executing code depending on
    /// the AVFilterContext->is_disabled value.
    /// </summary>
    SupportTimelineInternal = ffmpeg.AVFILTER_FLAG_SUPPORT_TIMELINE_INTERNAL,

    /// <summary>
    /// Handy mask to test whether the filter supports or no the timeline feature
    /// (internally or generically).
    /// </summary>
    SupportTimeline = ffmpeg.AVFILTER_FLAG_SUPPORT_TIMELINE,
}