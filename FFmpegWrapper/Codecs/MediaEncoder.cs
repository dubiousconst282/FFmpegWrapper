namespace FFmpeg.Wrapper;

public abstract unsafe class MediaEncoder : CodecBase
{

    /// <inheritdoc cref="AVCodecContext.bit_rate" />
    public int BitRate {
        get => (int)_ctx->bit_rate;
        set => SetOrThrowIfOpen(ref _ctx->bit_rate, value);
    }

    /// <inheritdoc cref="AVCodecContext.global_quality" />
    public int GlobalQuality {
        get => _ctx->global_quality;
        set => SetOrThrowIfOpen(ref _ctx->global_quality, value);
    }

    /// <inheritdoc cref="AVCodecContext.compression_level" />
    public int CompressionLevel {
        get => _ctx->compression_level;
        set => SetOrThrowIfOpen(ref _ctx->compression_level, value);
    }

    public MediaEncoder(AVCodecContext* ctx, AVMediaType expectedType, bool takeOwnership)
        : base(ctx, expectedType, takeOwnership) { }

    /// <summary> Sets a codec specific option. If it doesn't exist, throws <see cref="InvalidOperationException"/>. </summary>
    public void SetOption(string name, string value)
    {
        ffmpeg.av_opt_set(Handle->priv_data, name, value, 0).CheckError();
    }

    /// <summary> Sets the value for a generic codec option. Note that these values may be ignored or unbalanced for some codecs. </summary>
    /// <remarks> https://ffmpeg.org/ffmpeg-codecs.html#Codec-Options </remarks>
    public void SetGlobalOption(string name, string value)
    {
        ffmpeg.av_opt_set(Handle, name, value, 0).CheckError();
    }

    public bool ReceivePacket(MediaPacket pkt)
    {
        var result = (LavResult)ffmpeg.avcodec_receive_packet(Handle, pkt.Handle);

        if (result is not (LavResult.Success or LavResult.TryAgain or LavResult.EndOfFile)) {
            result.ThrowIfError("Could not encode packet");
        }
        return result >= 0;
    }
    public bool SendFrame(MediaFrame? frame)
    {
        var result = (LavResult)ffmpeg.avcodec_send_frame(Handle, frame == null ? null : frame.Handle);

        if (result != LavResult.Success && !(result == LavResult.EndOfFile && frame == null)) {
            result.ThrowIfError("Could not encode frame");
        }
        return result >= 0;
    }

    /// <summary> Returns a presentation timestamp (PTS) in terms of <see cref="CodecBase.TimeBase"/> for the given timespan. </summary>
    public long GetFramePts(TimeSpan time)
    {
        return GetFramePts(time.Ticks, new Rational(1, (int)TimeSpan.TicksPerSecond));
    }
    /// <summary> Rescales the given timestamp to be in terms of <see cref="CodecBase.TimeBase"/>. </summary>
    public long GetFramePts(long pts, Rational timeBase)
    {
        return ffmpeg.av_rescale_q(pts, timeBase, TimeBase);
    }
}
