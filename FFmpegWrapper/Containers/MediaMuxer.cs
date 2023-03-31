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
    private bool _iocLeaveOpen;

    private List<MediaStream> _streams = new List<MediaStream>();
    private MediaPacket? _tempPacket;

    public IReadOnlyList<MediaStream> Streams => _streams;

    public bool IsOpen { get; private set; } = false;

    public MediaMuxer(string filename)
    {
        fixed (AVFormatContext** fmtCtx = &_ctx) {
            ffmpeg.avformat_alloc_output_context2(fmtCtx, null, null, filename).CheckError("Could not allocate muxer");
        }
        ffmpeg.avio_open(&_ctx->pb, filename, ffmpeg.AVIO_FLAG_WRITE).CheckError("Could not open output file");
    }
    public MediaMuxer(IOContext ioc, string formatExtension, bool leaveOpen = true)
        : this(ioc, ContainerTypes.GetOutputFormat(formatExtension), leaveOpen) { }
    public MediaMuxer(IOContext ioc, AVOutputFormat* format, bool leaveOpen = true)
    {
        IOC = ioc;

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

        AVStream* stream = ffmpeg.avformat_new_stream(_ctx, encoder.Handle->codec);
        if (stream == null) {
            throw new OutOfMemoryException("Could not allocate stream");
        }
        stream->id = (int)_ctx->nb_streams - 1;
        stream->time_base = encoder.TimeBase;
        ffmpeg.avcodec_parameters_from_context(stream->codecpar, encoder.Handle).CheckError("Could not copy the encoder parameters to the stream.");

        //Some formats want stream headers to be separate.
        if ((_ctx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0) {
            encoder.Handle->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
        }

        var st = new MediaStream(stream);
        _streams.Add(st);
        return st;
    }

    /// <summary> Opens all streams and writes the container header. </summary>
    public void Open()
    {
        Open(null);
    }

    /// <summary> Opens all streams and writes the container header. </summary>
    /// <param name="options">
    /// Options passed to <see cref="ffmpeg.avformat_write_header(AVFormatContext*, AVDictionary**)"/>. 
    /// When this method returns, the value of this parameter will be destroyed and replaced with a dict containing options that were not found.
    /// </param>
    public void Open(AVDictionary** options)
    {
        ThrowIfDisposed();
        if (IsOpen) {
            throw new InvalidOperationException("Muxer is already open");
        }
        ffmpeg.avformat_write_header(_ctx, options).CheckError("Could not write header to output file");
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

        if (_streams[stream.Index] != stream) {
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