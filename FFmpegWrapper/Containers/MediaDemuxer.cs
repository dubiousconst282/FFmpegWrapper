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

    public TimeSpan? Duration => Helpers.GetTimeSpan(_ctx->duration, new Rational(1, ffmpeg.AV_TIME_BASE));

    /// <summary> An array of all streams in the file. </summary>
    public ImmutableArray<MediaStream> Streams { get; }

    /// <inheritdoc cref="AVFormatContext.metadata" />
    public MediaDictionary Metadata => new(&_ctx->metadata);

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