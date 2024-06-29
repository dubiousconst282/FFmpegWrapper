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
    readonly bool _ownsCtx;

    private List<(MediaStream Stream, MediaEncoder? Encoder)> _streams = new();
    private MediaPacket? _tempPacket;

    public IReadOnlyList<MediaStream> Streams => _streams.Select(s => s.Stream).ToList();

    /// <inheritdoc cref="AVFormatContext.metadata" />
    public MediaDictionary Metadata => new(&_ctx->metadata);

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

    /// <summary> Wraps a pointer to an open <see cref="AVFormatContext"/>. </summary>
    /// <param name="takeOwnership">True if <paramref name="ctx"/> should be freed when Dispose() is called.</param>
    public MediaMuxer(AVFormatContext* ctx, bool takeOwnership)
    {
        _ctx = ctx;
        _ownsCtx = takeOwnership;
    }

    /// <summary> Creates and adds a new stream to the muxed file. </summary>
    /// <remarks> The <paramref name="encoder"/> must not be open before this is called. </remarks>
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

    /// <summary>
    /// Creates and adds a new stream to the muxed file, copying the codec parameters from the source stream.
    /// </summary>
    public MediaStream AddStream(MediaStream srcStream)
    {
        ThrowIfDisposed();
        if (IsOpen) {
            throw new InvalidOperationException("Cannot add new streams once the muxer is open.");
        }

        AVStream* stream = ffmpeg.avformat_new_stream(_ctx, null);
        if (stream == null) {
            throw new OutOfMemoryException("Could not allocate stream");
        }

        ffmpeg.avcodec_parameters_copy(stream->codecpar, srcStream.Handle->codecpar).CheckError("Failed to copy codec parameters");
        stream->codecpar->codec_tag = 0;

        stream->id = (int)_ctx->nb_streams - 1;
        stream->time_base = srcStream.TimeBase;

        var st = new MediaStream(stream);
        _streams.Add((st, default));
        return st;
    }

    /// <summary> Opens all streams and writes the container header. </summary>
    /// <remarks> This method will also open all encoders passed to <see cref="AddStream(MediaEncoder)"/>. </remarks>
    public void Open()
    {
        Open(Enumerable.Empty<KeyValuePair<string, string>>(), true);
    }

    /// <inheritdoc cref="Open()" />
    /// <param name="options">A collection of AVFormatContext and muxer-private options. </param>
    /// <param name="ignoreUnknownOptions">When false, throws <see cref="InvalidOperationException" /> when <paramref name="options" /> contains unknown or invalid entries. </param>
    public void Open(IEnumerable<KeyValuePair<string, string>> options, bool ignoreUnknownOptions = false)
    {
        ThrowIfDisposed();
        if (IsOpen) {
            throw new InvalidOperationException("Muxer is already open.");
        }

        foreach (var (stream, encoder) in _streams) {
            if (encoder is null) continue;
            encoder.Open();
            ffmpeg.avcodec_parameters_from_context(stream.Handle->codecpar, encoder.Handle).CheckError("Could not copy the encoder parameters to the stream.");
        }

        AVDictionary* rawOpts = null;
        MediaDictionary.Populate(&rawOpts, options);

        ffmpeg.avformat_write_header(_ctx, &rawOpts).CheckError("Could not write header to output file");

        try {
            if (!ignoreUnknownOptions && ffmpeg.av_dict_count(rawOpts) > 0) {
                string invalidKeys = string.Join("', '", new MediaDictionary(&rawOpts).Select(e => e.Key));
                throw new InvalidOperationException($"Unknown or invalid muxer options (keys: '{invalidKeys}')");
            }
        } finally {
            ffmpeg.av_dict_free(&rawOpts);
        }
        IsOpen = true;
    }

    /// <summary> Muxes the given packet to the output file, ensuring correct interleaving. </summary>
    /// <remarks>
    /// This function will buffer the packets internally as needed to make sure the
    /// packets in the output file are properly interleaved, usually ordered by
    /// increasing dts.
    /// </remarks>
    /// <param name="packet">
    /// This parameter can be null (at any time, not just at the end), to flush the interleaving queues.
    /// <br/>
    /// The <see cref="MediaPacket.StreamIndex"/> field must be
    /// set to the index of the corresponding stream in <see cref="Streams"/>.
    /// <br/>
    /// The timestamps (PTS and DTS) must be set to correct values in the stream's timebase (unless the
    /// output format is flagged with the AVFMT_NOTIMESTAMPS flag, then they can be set to AV_NOPTS_VALUE).
    /// The dts for subsequent packets in one stream must be strictly increasing (unless the output format 
    /// is flagged with the AVFMT_TS_NONSTRICT, then they merely have to be nondecreasing).
    /// Duration should also be set if known.
    /// <br/>
    /// On return, the packet will have been reset.
    /// </param>
    public void Write(MediaPacket? packet)
    {
        ThrowIfNotOpen();

        ffmpeg.av_interleaved_write_frame(_ctx, packet == null ? null : packet.Handle).CheckError("Failed to write packet");
    }

    /// <summary> Encodes the given frame and muxes the resulting packets to the output file. </summary>
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
            ffmpeg.av_interleaved_write_frame(_ctx, _tempPacket.Handle).CheckError("Failed to write packet");
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

            if (_ownsCtx) {
                if (IOC == null) {
                    ffmpeg.avio_closep(&_ctx->pb);
                }
                ffmpeg.avformat_free_context(_ctx);
            }
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