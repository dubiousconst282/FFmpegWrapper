namespace FFmpeg.Wrapper;

using System.Text;

public unsafe struct PacketSideDataList
{
    readonly AVPacketSideData** _entries;
    readonly int* _count;

    public AVPacketSideData* EntriesPtr => *_entries;

    public Span<AVPacketSideData> Entries => new(EntriesPtr, Count);
    public int Count => *_count;

    public PacketSideDataList(AVPacketSideData** entries, int* count)
    {
        _entries = entries;
        _count = count;
    }

    /// <summary> Returns the side data entry with the given type, or null if not present. </summary>
    public AVPacketSideData* GetEntry(AVPacketSideDataType type)
    {
        return ffmpeg.av_packet_side_data_get(EntriesPtr, Count, type);
    }

    /// <summary> Returns the side data payload for an entry of the given type, or an empty span if not present. </summary>
    public ReadOnlySpan<byte> GetData(AVPacketSideDataType type)
    {
        var sideData = ffmpeg.av_packet_side_data_get(EntriesPtr, Count, type);
        return sideData == null ? default : new ReadOnlySpan<byte>(sideData->data, (int)sideData->size);
    }

    /// <summary>
    /// Returns the side data payload for an entry of the given type as a <typeparamref name="T"/> pointer, 
    /// or null if not present or if the payload is smaller than <c>sizeof(T)</c>.
    /// </summary>
    public T* GetDataPtr<T>(AVPacketSideDataType type) where T : unmanaged
    {
        var sideData = ffmpeg.av_packet_side_data_get(EntriesPtr, Count, type);
        return sideData == null || sideData->size < (ulong)sizeof(T) ? null : (T*)sideData->data;
    }

    /// <summary> Allocates or overwrites a side data entry. </summary>
    public AVPacketSideData* CreateEntry(AVPacketSideDataType type, int size)
    {
        var entry = ffmpeg.av_packet_side_data_new(_entries, _count, type, (ulong)size, 0);
        if (entry == null) {
            throw new OutOfMemoryException();
        }
        return entry;
    }

    /// <summary> Allocates or overwrites a side data entry and returns a span to its payload. </summary>
    public Span<byte> CreateData(AVPacketSideDataType type, int size)
    {
        var entry = CreateEntry(type, size);
        return new Span<byte>(entry->data, size);
    }

    /// <summary> Allocates or overwrites a side data entry and returns a pointer to its payload. </summary>
    /// <param name="size"> Size of the entry payload. Defaults to <c>sizeof(T)</c>. </param>
    public T* CreateDataPtr<T>(AVPacketSideDataType type, int size = 0) where T : unmanaged
    {
        var entry = CreateEntry(type, size <= 0 ? sizeof(T) : size);
        return (T*)entry->data;
    }


    /// <summary> Returns the value of an <see cref="AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX"/> entry. </summary>
    public int[]? GetDisplayMatrix()
    {
        var value = GetDataPtr<int_array9>(AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX);
        return value != null ? value->ToArray() : null;
    }

    public override string ToString()
    {
        var sb = new StringBuilder("[");

        for (int i = 0; i < Count; i++) {
            if (i != 0) sb.Append(", ");

            sb.Append(ffmpeg.av_packet_side_data_name(EntriesPtr[i].type));
            sb.Append($": {EntriesPtr[i].size} bytes");
        }
        return sb.Append(']').ToString();
    }
}