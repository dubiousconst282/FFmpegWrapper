namespace FFmpeg.Wrapper;

public unsafe class MediaMuxer : FFObject
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

    private List<(MediaStream Stream, MediaEncoder Encoder)> _streams = new();
    private MediaPacket? _tempPacket;

    public IReadOnlyList<MediaStream> Streams => _streams.Select(s => s.Stream).ToList();

    public MediaDictionary Metadata => new(&Handle->metadata);

    public bool IsOpen { get; private set; } = false;

    public MediaMuxer(string filename)
    {
        fixed (AVFormatContext** fmtCtx = &_ctx) {
            ffmpeg.avformat_alloc_output_context2(fmtCtx, null, null, filename).CheckError("Could not allocate muxer");
        }
        ffmpeg.avio_open(&_ctx->pb, filename, ffmpeg.AVIO_FLAG_WRITE).CheckError("Could not open output file");
    }

    public MediaMuxer(IOContext ioc, string formatExtension, bool leaveOpen = false)
        : this(ioc, ContainerTypes.GetOutputFormat(formatExtension), leaveOpen) { }

    public MediaMuxer(IOContext ioc, AVOutputFormat* format, bool leaveOpen = false)
    {
        IOC = ioc;
        _iocLeaveOpen = leaveOpen;

        _ctx = ffmpeg.avformat_alloc_context();
        if (_ctx == null) {
            throw new OutOfMemoryException("Could not allocate muxer");
        }
        _ctx->oformat = format;
        _ctx->pb = ioc.Handle;
    }

    /// <summary> Creates and adds a new stream to the muxed file. </summary>
    /// <remarks> The <paramref name="encoder"/> must be open before this is called. </remarks>
    public MediaStream AddStream(MediaEncoder encoder)
    {
        ThrowIfDisposed();
        if (IsOpen) {
            throw new InvalidOperationException("Cannot add new streams once the muxer is open.");
        }
        if (encoder.IsOpen) {
            //This is an unfortunate limitation, but the GlobalHeader flag must be set before the encoder is open.
            throw new InvalidOperationException("Cannot add stream with an already open encoder.");
        }

        AVStream* stream = ffmpeg.avformat_new_stream(_ctx, encoder.Handle->codec);
        if (stream == null) {
            throw new OutOfMemoryException("Could not allocate stream");
        }
        stream->id = (int)_ctx->nb_streams - 1;
        stream->time_base = encoder.TimeBase;

        //Some formats want stream headers to be separate.
        if ((_ctx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0) {
            encoder.Handle->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
        }

        var st = new MediaStream(stream);
        _streams.Add((st, encoder));
        return st;
    }

    /// <summary> Opens all streams and writes the container header. </summary>
    /// <remarks> This method will also open all encoders passed to <see cref="AddStream(MediaEncoder)"/>. </remarks>
    public void Open()
    {
        ThrowIfDisposed();
        if (IsOpen) {
            throw new InvalidOperationException("Muxer is already open.");
        }

        foreach (var (stream, encoder) in _streams) {
            encoder.Open();
            ffmpeg.avcodec_parameters_from_context(stream.Handle->codecpar, encoder.Handle).CheckError("Could not copy the encoder parameters to the stream.");
        }
        ffmpeg.avformat_write_header(_ctx, null).CheckError("Could not write header to output file");
        IsOpen = true;
    }

    public void Write(MediaPacket packet)
    {
        ThrowIfNotOpen();

        ffmpeg.av_interleaved_write_frame(_ctx, packet.Handle).CheckError("Failed to write frame");
    }

    public void EncodeAndWrite(MediaStream stream, MediaEncoder encoder, MediaFrame? frame)
    {
        ThrowIfNotOpen();

        if (_streams[stream.Index].Stream != stream) {
            throw new ArgumentException("Specified stream is not owned by the muxer.");
        }
        _tempPacket ??= new();

        encoder.SendFrame(frame);

        while (encoder.ReceivePacket(_tempPacket)) {
            _tempPacket.RescaleTS(encoder.TimeBase, stream.TimeBase);
            _tempPacket.StreamIndex = stream.Index;
            ffmpeg.av_interleaved_write_frame(_ctx, _tempPacket.Handle);
        }
    }

    private void ThrowIfNotOpen()
    {
        ThrowIfDisposed();

        if (!IsOpen) {
            throw new InvalidOperationException("Muxer is not open");
        }
    }

    protected override void Free()
    {
        if (_ctx != null) {
            ffmpeg.av_write_trailer(_ctx);
            ffmpeg.avformat_free_context(_ctx);
            _ctx = null;

            if (!_iocLeaveOpen) {
                IOC?.Dispose();
            }
            _tempPacket?.Dispose();
        }
    }
    private void ThrowIfDisposed()
    {
        if (_ctx == null) {
            throw new ObjectDisposedException(nameof(MediaMuxer));
        }
    }
}