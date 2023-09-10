namespace FFmpeg.Wrapper;

using System.Collections.Generic;

using static AVOptionType;

/// <summary> Represents an option accepted by a ffmpeg object. </summary>
public unsafe readonly struct ContextOption
{
    public AVOption* Handle { get; }

    public string Name => Helpers.PtrToStringUTF8(Handle->name)!;
    public string Description => Helpers.PtrToStringUTF8(Handle->help)!;
    public AVOptionType Type => Handle->type;
    public double MinValue => Handle->min;
    public double MaxValue => Handle->max;

    /// <summary> Offset to the field containing this option, relative to the object pointer. </summary>
    public int Offset => Handle->offset;

    //TODO: Expose Option.DefaultValue
    //public OptionValue? DefaultValue => throw new NotImplementedException();

    internal ContextOption(AVOption* handle) => Handle = handle;

    /// <summary> Returns a list of acceptable pre-defined input values. </summary>
    public IReadOnlyList<ContextOption> GetNamedValues()
    {
        if (Handle->unit == null) {
            return Array.Empty<ContextOption>();
        }
        var list = new List<ContextOption>();

        //The AVOption documentation says that AVClass options must be declared in
        //a static null terminated array, so this should be mostly fine.
        for (AVOption* opt = Handle + 1; opt->name != null; opt++) {
            if (opt->type == AV_OPT_TYPE_CONST && opt->unit == Handle->unit) {
                list.Add(new ContextOption(opt));
            }
        }
        return list;
    }
    /// <summary> Sets the field of <paramref name="obj"/> with the given name to <paramref name="value"/>. </summary>
    /// <remarks>
    /// In case <paramref name="value"/> is a string and the field is not
    /// of a string type, the given string will be parsed.
    /// SI postfixes and some named scalars are supported. <br/>
    /// If the field is of a numeric type, it has to be a numeric or named scalar.
    /// Behavior with more than one scalar and +- infix operators is undefined. <br/>
    /// If the field is of a flags type, it has to be a sequence of numeric
    /// scalars or named flags separated by '+' or '-'. Prefixing a flag with '+' 
    /// causes it to be set without affecting the other flags; similarly, '-' unsets a flag. <br/>
    /// If the field is of a dictionary type, it has to be a ':' separated list of key = value parameters.
    /// Values containing ':' special characters must be escaped. <br/>
    /// </remarks>
    /// <param name="obj"> A struct whose first element is a pointer to an AVClass. </param>
    /// <param name="name"> The name of the field to set. </param>
    /// <param name="value"></param>
    public static unsafe void Set(void* obj, string name, OptionValue value, bool searchChildren)
    {
        int flags = searchChildren ? ffmpeg.AV_OPT_SEARCH_CHILDREN : 0;
        int ret = value.BoxedValue switch {
            string v    => ffmpeg.av_opt_set(obj, name, v, flags),
            long v      => ffmpeg.av_opt_set_int(obj, name, v, flags),
            double v    => ffmpeg.av_opt_set_double(obj, name, v, flags),
            Rational v  => ffmpeg.av_opt_set_q(obj, name, v, flags),
            ChannelLayout v => ffmpeg.av_opt_set_chlayout(obj, name, &v.Native, flags),
            AVPixelFormat v => ffmpeg.av_opt_set_pixel_fmt(obj, name, v, flags),
            AVSampleFormat v => ffmpeg.av_opt_set_sample_fmt(obj, name, v, flags),
            byte[] v        => SetBinary(v)
        };
        if (ret < 0) {
            string className = Helpers.PtrToStringUTF8((*(AVClass**)obj)->class_name)!;
            ret.ThrowError($"Invalid option for {className} (trying to set {name} to {value.Type})");
        }

        int SetBinary(byte[] data)
        {
            fixed (byte* pData = data) {
                return ffmpeg.av_opt_set_bin(obj, name, pData, data.Length, flags);
            }
        }
    }

    /// <summary> Gets the value of an option in <paramref name="obj"/> as a string. </summary>
    public static string? GetAsString(void* obj, string name, bool searchChildren)
    {
        int flags = searchChildren ? ffmpeg.AV_OPT_SEARCH_CHILDREN : 0;

        byte* value;
        ffmpeg.av_opt_get(obj, name, flags, &value);

        string? str = Helpers.PtrToStringUTF8(value);
        ffmpeg.av_free(value);
        return str;
    }

    /// <summary> Returns a list of options accepted by the specified ffmpeg object. </summary>
    /// <param name="removeAliases">Remove options that are short aliases to another.</param>
    /// <param name="skipDefaults"> Skip options whose value in <paramref name="obj"/> are set to default. </param>
    public static IReadOnlyList<ContextOption> GetOptions(void* obj, bool removeAliases = true, bool skipDefaults = false)
    {
        var opts = new List<ContextOption>();

        AVOption* iter = null;
        while ((iter = ffmpeg.av_opt_next(obj, iter)) != null) {
            if (iter->type == AV_OPT_TYPE_CONST || (skipDefaults && ffmpeg.av_opt_is_set_to_default(obj, iter) != 0)) continue;

            opts.Add(new ContextOption(iter));
        }

        if (!removeAliases) {
            return opts;
        }

        opts.Sort((a, b) => a.Offset - b.Offset);

        int nextIgnoredOffset = -1;
        int j = 0;

        for (int i = 0; i < opts.Count; i++) {
            var opt = opts[i];

            while (i + 1 < opts.Count && opts[i + 1].Offset == opt.Offset) {
                var aliasOpt = opts[i + 1];

                if (aliasOpt.Type == AV_OPT_TYPE_IMAGE_SIZE || aliasOpt.Name.Length > opt.Name.Length) {
                    opt = aliasOpt;
                }
                i++;
            }

            if (opt.Offset == nextIgnoredOffset) continue;

            if (opt.Type == AV_OPT_TYPE_IMAGE_SIZE) {
                nextIgnoredOffset = opt.Offset + 4; //Ignore next height option
            }
            opts[j++] = opt;
        }
        
        opts.RemoveRange(j, opts.Count - j);
        return opts;
    }

    public override string ToString() => Name + ": " + Type.ToString().ToLower().Substring("AV_OPT_TYPE_".Length);
}

public readonly struct OptionValue
{
    readonly object _value;

    public object BoxedValue => _value;

    private OptionValue(object obj) { _value = obj; }

    public AVOptionType Type => _value switch {
        string => AV_OPT_TYPE_STRING,
        long => AV_OPT_TYPE_INT,
        double => AV_OPT_TYPE_DOUBLE,
        Rational => AV_OPT_TYPE_RATIONAL,
        ChannelLayout => AV_OPT_TYPE_CHLAYOUT,
        AVPixelFormat => AV_OPT_TYPE_PIXEL_FMT,
        AVSampleFormat => AV_OPT_TYPE_SAMPLE_FMT,
        byte[]         => AV_OPT_TYPE_BINARY
    };

    public string AsString() => (string)_value;
    public long AsInteger() => (long)_value;
    public double AsDouble() => (double)_value;
    public Rational AsRational() => (Rational)_value;
    public ChannelLayout AsChannelLayout() => (ChannelLayout)_value;
    public AVPixelFormat AsPixelFormat() => (AVPixelFormat)_value;
    public AVSampleFormat AsSampleFormat() => (AVSampleFormat)_value;
    public byte[] AsBinary() => (byte[])_value;

    public override string ToString() => _value.ToString();

    public static implicit operator OptionValue(string val) => new(val);
    public static implicit operator OptionValue(long val) => new(val);
    public static implicit operator OptionValue(double val) => new(val);
    public static implicit operator OptionValue(Rational val) => new(val);
    public static implicit operator OptionValue(ChannelLayout val) => new(val);
    public static implicit operator OptionValue(AVPixelFormat val) => new(val);
    public static implicit operator OptionValue(AVSampleFormat val) => new(val);
    public static implicit operator OptionValue(byte[] val) => new(val);
}