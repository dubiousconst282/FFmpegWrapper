namespace FFmpeg.Wrapper;

using System.Collections;

using Entry = KeyValuePair<string, string>;

/// <summary> Wrapper for an existing <see cref="AVDictionary"/>. </summary>
public unsafe struct MediaDictionary : IEnumerable<Entry>
{
    readonly AVDictionary** _target;
    public AVDictionary* Handle => *_target;

    public int Count => ffmpeg.av_dict_count(Handle);

    /// <exception cref="KeyNotFoundException"></exception>
    public string this[string key] {
        get => GetValue(key) ?? throw new KeyNotFoundException();
        set => SetValue(key, value);
    }

    public MediaDictionary(AVDictionary** target)
    {
        _target = target;
    }

    /// <summary> Gets the value associated with the given key, or null if there is no match. </summary>
    public string? GetValue(string key, bool matchCase = false, bool matchPrefix = false)
    {
        int flags = 0;
        flags |= matchCase ? ffmpeg.AV_DICT_MATCH_CASE : 0;
        flags |= matchPrefix ? ffmpeg.AV_DICT_IGNORE_SUFFIX : 0;

        var entry = ffmpeg.av_dict_get(Handle, key, null, flags);
        return entry == null ? null : Helpers.PtrToStringUTF8(entry->value);
    }

    /// <summary> Sets the value associated with the given key, overwriting it if necessary. </summary>
    public void SetValue(string key, string value, bool allowMultiple = false)
    {
        int flags = 0;
        flags |= allowMultiple ? ffmpeg.AV_DICT_MULTIKEY : 0;

        ffmpeg.av_dict_set(_target, key, value, flags).CheckError();
    }

    public void Remove(string key)
    {
        ffmpeg.av_dict_set(_target, key, null, 0).CheckError();
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator() { _dict = Handle, _entry = null };
    }

    internal static void Populate(AVDictionary** dict, IEnumerable<Entry>? options)
    {
        if (options == null) return;

        foreach (var entry in options) {
            ffmpeg.av_dict_set(dict, entry.Key, entry.Value, 0);
        }
    }

    IEnumerator<Entry> IEnumerable<Entry>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<Entry>
    {
        internal AVDictionary* _dict;
        internal AVDictionaryEntry* _entry;

        public Entry Current {
            get {
                string key = Helpers.PtrToStringUTF8(_entry->key)!;
                string val = Helpers.PtrToStringUTF8(_entry->value)!;
                return new Entry(key, val);
            }
        }
        public bool MoveNext()
        {
            return (_entry = ffmpeg.av_dict_iterate(_dict, _entry)) != null;
        }

        object IEnumerator.Current => throw new NotImplementedException();
        void IEnumerator.Reset() => throw new NotSupportedException();
        void IDisposable.Dispose() { }
    }
}