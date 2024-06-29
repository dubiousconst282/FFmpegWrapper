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
    readonly bool _iocLeaveOpen;
    readonly bool _ownsCtx;

    public TimeSpan? Duration => Helpers.GetTimeSpan(_ctx->duration, new Rational(1, ffmpeg.AV_TIME_BASE));

    /// <summary> An array of all streams in the file. </summary>
    public ImmutableArray<MediaStream> Streams { get; }

    /// <inheritdoc cref="AVFormatContext.metadata" />
    public MediaDictionary Metadata => new(&_ctx->metadata);

    public bool CanSeek => _ctx->pb->seek.Pointer != IntPtr.Zero;

    /// <summary> Opens an existing resource URL for demuxing. </summary>
    /// <remarks>
    /// Note that this constructor accepts URLs for other than files, as supported by FFmpeg. <br/>
    /// If that is not desirable, ensure that <paramref name="url"/> points to a valid file path prior to instantiation (via <see cref="File.Exists(string)"/>), 
    /// or use <see cref="MediaDemuxer(string, IEnumerable{KeyValuePair{string, string}})"/> with the
    /// <c>protocol_whitelist=file</c> option.
    /// </remarks>
    public MediaDemuxer(string url)
        : this(CreateContext(url, null, null), takeOwnership: true) { }

    public MediaDemuxer(IOContext ioc, bool leaveOpen = false)
        : this(CreateContext(null, ioc.Handle, null), takeOwnership: true)
    {
        IOC = ioc;
        _iocLeaveOpen = leaveOpen;
    }

    /// <summary> Opens an existing resource URL for demuxing. </summary>
    /// <remarks> See https://ffmpeg.org/ffmpeg-formats.html, https://ffmpeg.org/ffmpeg-protocols.html </remarks>
    /// <param name="options"> A dictionary filled with AVFormatContext and demuxer-private options. </param>
    public MediaDemuxer(string url, IEnumerable<KeyValuePair<string, string>> options)
        : this(CreateContext(url, null, options), takeOwnership: true) { }

    /// <summary> Wraps a pointer to an open <see cref="AVFormatContext"/>. </summary>
    /// <param name="takeOwnership">True if <paramref name="ctx"/> should be freed when Dispose() is called.</param>
    public MediaDemuxer(AVFormatContext* ctx, bool takeOwnership)
    {
        _ctx = ctx;
        _ownsCtx = takeOwnership;

        var streams = ImmutableArray.CreateBuilder<MediaStream>((int)_ctx->nb_streams);
        for (int i = 0; i < _ctx->nb_streams; i++) {
            streams.Add(new MediaStream(_ctx->streams[i]));
        }
        Streams = streams.MoveToImmutable();
    }
    
    private static AVFormatContext* CreateContext(string? url, AVIOContext* pb, IEnumerable<KeyValuePair<string, string>>? options)
    {
        var ctx = ffmpeg.avformat_alloc_context();
        if (ctx == null) {
            throw new OutOfMemoryException("Could not allocate demuxer.");
        }

        ctx->pb = pb;

        AVDictionary* rawOpts = null;
        MediaDictionary.Populate(&rawOpts, options);

        ffmpeg.avformat_open_input(&ctx, url, null, &rawOpts).CheckError("Could not open input");

        try {
            if (ffmpeg.av_dict_count(rawOpts) > 0) {
                string invalidKeys = string.Join("', '", new MediaDictionary(&rawOpts).Select(e => e.Key));
                throw new InvalidOperationException($"Unknown or invalid demuxer options (keys: '{invalidKeys}')");
            }
        } finally {
            ffmpeg.av_dict_free(&rawOpts);
        }

        ffmpeg.avformat_find_stream_info(ctx, null).CheckError("Could not find stream information");
        return ctx;
    }

    /// <summary> Find the "best" stream in the file. The best stream is determined according to various heuristics as the most likely to be what the user expects. </summary>
    public MediaStream? FindBestStream(AVMediaType type)
    {
        ThrowIfDisposed();

        int index = ffmpeg.av_find_best_stream(_ctx, type, -1, -1, null, 0);
        return index < 0 ? null : Streams[index];
    }

    /// <summary> Creates a decoder for the given audio or video stream. </summary>
    /// <param name="open">
    /// True to call <see cref="CodecBase.Open()" /> before returning the decoder.
    /// Should be set to false if extra setup (e.g. hardware acceleration) is needed before opening.
    /// </param>
    /// <returns></returns>
    public MediaDecoder CreateStreamDecoder(MediaStream stream, bool open = true)
    {
        ThrowIfDisposed();

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
        
        // Fixup some unset properties for consistency 
        decoder.TimeBase = stream.TimeBase;

        if (stream.Type == MediaTypes.Video && decoder.FrameRate == Rational.Zero) {
            decoder.FrameRate = GuessFrameRate(stream);
        }

        if (open) decoder.Open();

        return decoder;
    }

    /// <inheritdoc cref="ffmpeg.av_read_frame(AVFormatContext*, AVPacket*)"/>
    public bool Read(MediaPacket packet)
    {
        ThrowIfDisposed();

        int result = ffmpeg.av_read_frame(_ctx, packet.UnrefAndGetHandle());

        if (result < 0 && result != ffmpeg.AVERROR_EOF) {
            result.ThrowError("Failed to read packet");
        }
        return result >= 0;
    }

    /// <summary> Seeks the demuxer to somewhere near <paramref name="timestamp"/>, according to <paramref name="options"/>. </summary>
    /// <param name="stream">The stream to seek in. If null, a default stream is selected.</param>
    /// <remarks> If this method returns true, all open stream decoders must be flushed by calling <see cref="CodecBase.Flush"/>. </remarks>
    /// <exception cref="InvalidOperationException">If the underlying IO context doesn't support seeks.</exception>
    /// <exception cref="ArgumentException">If <paramref name="stream"/> is not owned by the demuxer.</exception>
    public bool Seek(TimeSpan timestamp, SeekOptions options, MediaStream? stream = default)
    {
        ThrowIfDisposed();

        if (!CanSeek) {
            throw new InvalidOperationException("Backing IO context is not seekable.");
        }

        int streamIndex;
        long ts;
        if (stream is { }) {
            streamIndex = stream.Index;
            if (Streams[streamIndex] != stream) {
                throw new ArgumentException("Specified stream is not owned by the demuxer.");
            }
            ts = ffmpeg.av_rescale_q(timestamp.Ticks, new Rational(1, (int)TimeSpan.TicksPerSecond), stream.TimeBase);
        } else {
            streamIndex = -1;
            ts = ffmpeg.av_rescale(timestamp.Ticks, ffmpeg.AV_TIME_BASE, TimeSpan.TicksPerSecond);
        }
        return ffmpeg.av_seek_frame(_ctx, streamIndex, ts, (int)options) >= 0;
    }

    /// <inheritdoc cref="ffmpeg.av_guess_frame_rate(AVFormatContext*, AVStream*, AVFrame*)"/>
    public Rational GuessFrameRate(MediaStream stream)
    {
        ThrowIfDisposed();

        if (Streams[stream.Index] != stream) {
            throw new ArgumentException("Specified stream is not owned by the demuxer.");
        }

        return ffmpeg.av_guess_frame_rate(_ctx, stream.Handle, null);
    }

    protected override void Free()
    {
        if (_ctx != null && _ownsCtx) {
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
[Flags]
public enum SeekOptions
{
    /// <summary> Seek to the nearest keyframe after or at the requested timestamp. </summary>
    Forward = 0,

    /// <summary> Seek to the nearest keyframe before or at the requested timestamp. </summary>
    Backward = ffmpeg.AVSEEK_FLAG_BACKWARD,

    /// <summary> Allow seeking to non-keyframes. This may cause decoders to fail or output corrupt frames. </summary>
    AllowNonKeyFrames = ffmpeg.AVSEEK_FLAG_ANY,
}