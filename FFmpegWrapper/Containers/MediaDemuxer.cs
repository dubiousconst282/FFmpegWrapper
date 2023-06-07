using System.Collections.Immutable;

namespace FFmpeg.Wrapper;

public unsafe class MediaDemuxer : FFObject
{
    private AVFormatContext* _ctx;

    public AVFormatContext* Handle {
        get {
            ThrowIfDisposed();
            return _ctx;
        }
    }

    public IOContext? IOC { get; }
    private bool _iocLeaveOpen;

    public TimeSpan? Duration => Helpers.GetTimeSpan(_ctx->duration, new() { num = 1, den = ffmpeg.AV_TIME_BASE });

    public ImmutableArray<MediaStream> Streams { get; }

    public MediaDictionary Metadata => new(&Handle->metadata);

    public bool CanSeek => _ctx->pb->seek.Pointer != IntPtr.Zero;

    public MediaDemuxer(string filename)
        : this(filename, null) { }

    public MediaDemuxer(IOContext ioc, bool leaveOpen = false)
        : this(null, ioc.Handle)
    {
        IOC = ioc;
        _iocLeaveOpen = leaveOpen;
    }
    
    private MediaDemuxer(string? url, AVIOContext* pb)
    {
        _ctx = ffmpeg.avformat_alloc_context();
        if (_ctx == null) {
            throw new OutOfMemoryException("Could not allocate demuxer.");
        }

        _ctx->pb = pb;
        fixed (AVFormatContext** c = &_ctx) {
            ffmpeg.avformat_open_input(c, url, null, null).CheckError("Could not open input");
        }

        ffmpeg.avformat_find_stream_info(_ctx, null).CheckError("Could not find stream information");

        var streams = ImmutableArray.CreateBuilder<MediaStream>((int)_ctx->nb_streams);
        for (int i = 0; i < _ctx->nb_streams; i++) {
            streams.Add(new MediaStream(_ctx->streams[i]));
        }
        Streams = streams.MoveToImmutable();
    }

    /// <summary> Find the "best" stream in the file. The best stream is determined according to various heuristics as the most likely to be what the user expects. </summary>
    public MediaStream? FindBestStream(AVMediaType type)
    {
        int index = ffmpeg.av_find_best_stream(_ctx, type, -1, -1, null, 0);
        return index < 0 ? null : Streams[index];
    }

    public MediaDecoder CreateStreamDecoder(MediaStream stream, bool open = true)
    {
        if (Streams[stream.Index] != stream) {
            throw new ArgumentException("Specified stream is not owned by the demuxer.");
        }

        var codecId = stream.Handle->codecpar->codec_id;
        var decoder = stream.Type switch {
            MediaTypes.Audio => new AudioDecoder(codecId) as MediaDecoder,
            MediaTypes.Video => new VideoDecoder(codecId),
            _ => throw new NotSupportedException($"Stream type {stream.Type} is not supported."),
        };
        ffmpeg.avcodec_parameters_to_context(decoder.Handle, stream.Handle->codecpar).CheckError("Could not copy stream parameters to the decoder.");

        if (open) decoder.Open();

        return decoder;
    }

    /// <inheritdoc cref="ffmpeg.av_read_frame(AVFormatContext*, AVPacket*)"/>
    public bool Read(MediaPacket packet)
    {
        ThrowIfDisposed();
        int result = ffmpeg.av_read_frame(_ctx, packet.UnrefAndGetHandle());

        if (result != 0 && result != ffmpeg.AVERROR_EOF) {
            result.ThrowError("Failed to read packet");
        }
        return result == 0;
    }

    /// <summary> Seeks the demuxer to some keyframe near but not later than <paramref name="timestamp"/>. </summary>
    /// <remarks> If this method returns true, all open stream decoders should be flushed by calling <see cref="CodecBase.Flush"/>. </remarks>
    /// <exception cref="InvalidOperationException">If the underlying IO context doesn't support seeks.</exception>
    public bool Seek(TimeSpan timestamp)
    {
        ThrowIfDisposed();

        if (!CanSeek) {
            throw new InvalidOperationException("Backing IO context is not seekable.");
        }
        long ts = ffmpeg.av_rescale(timestamp.Ticks, ffmpeg.AV_TIME_BASE, TimeSpan.TicksPerSecond);
        return ffmpeg.av_seek_frame(_ctx, -1, ts, ffmpeg.AVSEEK_FLAG_BACKWARD) == 0;
    }

    protected override void Free()
    {
        if (_ctx != null) {
            fixed (AVFormatContext** c = &_ctx) ffmpeg.avformat_close_input(c);
        }
        if (!_iocLeaveOpen) {
            IOC?.Dispose();
        }
    }
    private void ThrowIfDisposed()
    {
        if (_ctx == null) {
            throw new ObjectDisposedException(nameof(MediaDemuxer));
        }
    }
}