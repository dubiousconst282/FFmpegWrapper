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

    public long? PresentationTimestamp {
        get => Helpers.GetTimestamp(_pkt->pts);
        set => Helpers.SetTimestamp(ref _pkt->pts, value);
    }
    public long? DecompressionTimestamp {
        get => Helpers.GetTimestamp(_pkt->dts);
        set => Helpers.SetTimestamp(ref _pkt->dts, value);
    }

    public long Duration {
        get => _pkt->duration;
        set => _pkt->duration = value;
    }
    public int StreamIndex {
        get => _pkt->stream_index;
        set => _pkt->stream_index = value;
    }

    public Span<byte> Data => new Span<byte>(_pkt->data, _pkt->size);

    public MediaPacket()
    {
        _pkt = ffmpeg.av_packet_alloc();
    }

    /// <inheritdoc cref="ffmpeg.av_packet_rescale_ts(AVPacket*, AVRational, AVRational)"/>
    public void RescaleTS(AVRational sourceBase, AVRational destBase)
    {
        ffmpeg.av_packet_rescale_ts(Handle, sourceBase, destBase);
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
