namespace FFmpeg.Wrapper;

using System.Text;

public unsafe struct FrameSideDataList
{
    readonly AVFrame* _frame;

    public AVFrame* Frame => _frame;
    public int Count => _frame->nb_side_data;

    public FrameSideData this[int index] {
        get {
            if ((uint)index > (uint)Count) {
                throw new ArgumentOutOfRangeException();
            }
            return new(_frame->side_data[index]);
        }
    }

    public FrameSideDataList(AVFrame* frame)
    {
        _frame = frame;
    }

    /// <summary> Returns the side data entry for the given type, or null if not present. </summary>
    public FrameSideData? Get(AVFrameSideDataType type)
    {
        AVFrameSideData* entry = ffmpeg.av_frame_get_side_data(_frame, type);
        return entry != null ? new FrameSideData(entry) : null;
    }

    /// <summary> Allocates and adds a new a side data entry. </summary>
    public FrameSideData Add(AVFrameSideDataType type, int size)
    {
        var entry = ffmpeg.av_frame_new_side_data(_frame, type, (ulong)size);
        if (entry == null) {
            throw new OutOfMemoryException();
        }
        return new FrameSideData(entry);
    }

    public bool Remove(AVFrameSideDataType type)
    {
        int prevCount = Count;
        ffmpeg.av_frame_remove_side_data(_frame, type);
        return Count != prevCount;
    }

    public void Clear()
    {
        // https://github.com/FFmpeg/FFmpeg/blob/4e120fbbbd087c3acbad6ce2e8c7b1262a5c8632/libavfilter/f_sidedata.c#L117
        while (_frame->nb_side_data != 0) {
            ffmpeg.av_frame_remove_side_data(_frame, _frame->side_data[0]->type);
        }
    }

    /// <summary> Returns the value of an <see cref="AVFrameSideDataType.AV_FRAME_DATA_DISPLAYMATRIX"/> entry. </summary>
    public int[]? GetDisplayMatrix()
    {
        var entry = Get(AVFrameSideDataType.AV_FRAME_DATA_DISPLAYMATRIX);
        return entry?.GetDataRef<int_array9>().ToArray();
    }

    public override string ToString()
    {
        var sb = new StringBuilder("[");

        for (int i = 0; i < Count; i++) {
            if (i != 0) sb.Append(", ");
            sb.Append(this[i].ToString());
        }
        return sb.Append(']').ToString();
    }
}

public unsafe struct FrameSideData(AVFrameSideData* handle)
{
    public AVFrameSideData* Handle { get; } = handle;

    public Span<byte> Data => new Span<byte>(Handle->data, checked((int)Handle->size));
    public MediaDictionary Metadata => new(&Handle->metadata);
    public AVFrameSideDataType Type => Handle->type;

    /// <summary>
    /// Returns the side data payload reinterpreted as a <typeparamref name="T"/> pointer, 
    /// or null if the payload is smaller than <c>sizeof(T)</c>.
    /// </summary>
    public T* GetDataPtr<T>() where T : unmanaged
    {
        return Handle->size < (ulong)sizeof(T) ? null : (T*)Handle->data;
    }

    /// <summary>
    /// Returns the side data payload reinterpreted as a <typeparamref name="T"/> reference, 
    /// or throws <see cref="InvalidCastException"/> if the payload is smaller than <c>sizeof(T)</c>.
    /// </summary>
    public ref T GetDataRef<T>() where T : unmanaged
    {
        if (Handle->size < (ulong)sizeof(T)) {
            throw new InvalidCastException();
        }
        return ref *(T*)Handle->data;
    }

    public override string ToString() => $"{ffmpeg.av_frame_side_data_name(Type)}: {Handle->size} bytes";
}