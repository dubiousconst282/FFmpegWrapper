namespace FFmpeg.Wrapper;

public unsafe class MediaPacket : FFObject
{
    private AVPacket* _pkt;

    public AVPacket* Handle {
        get {
            ThrowIfDisposed();
            return _pkt;
        }
    }

    /// <summary>
    /// Presentation timestamp in <see cref="MediaStream.TimeBase"/> units; 
    /// the time at which the decompressed packet will be presented to the user. <br/>
    /// 
    /// Can be <see langword="null"/> if it is not stored in the file. MUST be larger
    /// or equal to <see cref="DecompressionTimestamp"/> as presentation cannot happen before
    /// decompression, unless one wants to view hex dumps.  <br/>
    /// 
    /// Some formats misuse the terms dts and pts/cts to mean something different.
    /// Such timestamps must be converted to true pts/dts before they are stored in AVPacket.
    /// </summary>
    public long? PresentationTimestamp {
        get => Helpers.GetPTS(_pkt->pts);
        set => Helpers.SetPTS(ref _pkt->pts, value);
    }
    public long? DecompressionTimestamp {
        get => Helpers.GetPTS(_pkt->dts);
        set => Helpers.SetPTS(ref _pkt->dts, value);
    }

    /// <summary> Duration of this packet in <see cref="MediaStream.TimeBase"/> units, 0 if unknown. Equals next_pts - this_pts in presentation order.  </summary>
    public long Duration {
        get => _pkt->duration;
        set => _pkt->duration = value;
    }
    public int StreamIndex {
        get => _pkt->stream_index;
        set => _pkt->stream_index = value;
    }

    /// <summary> Whether this packet contains a key-frame. (Checks if AV_PKT_FLAG_KEY is set) </summary>
    public bool IsKeyFrame {
        get => (_pkt->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0;
        set => _pkt->flags = value ? (_pkt->flags | ffmpeg.AV_PKT_FLAG_KEY) : (_pkt->flags & ~ffmpeg.AV_PKT_FLAG_KEY);
    }

    /// <inheritdoc cref="AVPacket.pos"/>
    public long BytePosition {
        get => _pkt->pos;
        set => _pkt->pos = value;
    }

    public Span<byte> Data {
        get => new(_pkt->data, _pkt->size);
    }

    public PacketSideDataList SideData => new(&_pkt->side_data, &_pkt->side_data_elems);

    public MediaPacket()
    {
        _pkt = ffmpeg.av_packet_alloc();

        if (_pkt == null) {
            throw new OutOfMemoryException();
        }
    }
    public MediaPacket(int size)
        : this()
    {
        ffmpeg.av_new_packet(_pkt, size).CheckError("Failed to allocate packet buffer");
    }

    /// <summary> Copies the specified data span to the packet, ensuring buffer space. </summary>
    public void SetData(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();

        if (_pkt->buf == null || _pkt->buf->size < (ulong)data.Length + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE) {
            byte* buffer = (byte*)ffmpeg.av_malloc((ulong)data.Length + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE);
            if (buffer == null) {
                throw new OutOfMemoryException();
            }
            ffmpeg.av_packet_from_data(_pkt, buffer, data.Length).CheckError("Failed to allocate packet buffer");
        }
        _pkt->size = data.Length;
        data.CopyTo(Data);
    }

    /// <inheritdoc cref="ffmpeg.av_packet_rescale_ts(AVPacket*, AVRational, AVRational)"/>
    public void RescaleTS(Rational sourceBase, Rational destBase)
    {
        ffmpeg.av_packet_rescale_ts(Handle, sourceBase, destBase);
    }

    /// <summary> Returns the underlying packet pointer after calling av_packet_unref() on it. </summary>
    public AVPacket* UnrefAndGetHandle()
    {
        ThrowIfDisposed();

        ffmpeg.av_packet_unref(_pkt);
        return _pkt;
    }

    protected override void Free()
    {
        fixed (AVPacket** pkt = &_pkt) {
            ffmpeg.av_packet_free(pkt);
        }
    }
    private void ThrowIfDisposed()
    {
        if (_pkt == null) {
            throw new ObjectDisposedException(nameof(MediaPacket));
        }
    }
}
