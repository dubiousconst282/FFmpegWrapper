namespace FFmpeg.Wrapper;

public abstract unsafe class MediaEncoder : CodecBase
{
    public int BitRate {
        get => (int)_ctx->bit_rate;
        set => SetOrThrowIfOpen(ref _ctx->bit_rate, value);
    }

    internal MediaEncoder(AVCodecID codecId, AVMediaType parentType)
        : base(FindCoder(codecId, parentType, isEncoder: true)) { }

    public void SetOption(string name, string value)
    {
        ffmpeg.av_opt_set(Handle->priv_data, name, value, 0).CheckError();
    }

    public bool ReceivePacket(MediaPacket pkt)
    {
        var result = (LavResult)ffmpeg.avcodec_receive_packet(Handle, pkt.Handle);

        if (result is not (LavResult.Success or LavResult.TryAgain or LavResult.EndOfFile)) {
            result.ThrowIfError("Could not encode packet");
        }
        return result == 0;
    }
    public bool SendFrame(MediaFrame? frame)
    {
        var result = (LavResult)ffmpeg.avcodec_send_frame(Handle, frame == null ? null : frame.Handle);

        if (result != LavResult.Success && !(result == LavResult.EndOfFile && frame == null)) {
            result.ThrowIfError("Could not encode frame");
        }
        return result == 0;
    }
}
