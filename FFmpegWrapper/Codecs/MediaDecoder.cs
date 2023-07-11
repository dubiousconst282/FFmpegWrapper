namespace FFmpeg.Wrapper;

public abstract unsafe class MediaDecoder : CodecBase
{
    public MediaDecoder(AVCodecContext* ctx, AVMediaType expectedType, bool takeOwnership)
        : base(ctx, expectedType, takeOwnership) { }

    public void SendPacket(MediaPacket? packet)
    {
        var result = TrySendPacket(packet);

        if (result != LavResult.Success && !(result == LavResult.EndOfFile && packet == null)) {
            result.ThrowIfError("Could not decode packet");
        }
    }

    /// <inheritdoc cref="ffmpeg.avcodec_send_packet(AVCodecContext*, AVPacket*)"/>
    public LavResult TrySendPacket(MediaPacket? packet)
    {
        return (LavResult)ffmpeg.avcodec_send_packet(Handle, packet == null ? null : packet.Handle);
    }

    public bool ReceiveFrame(MediaFrame frame)
    {
        var result = (LavResult)ffmpeg.avcodec_receive_frame(Handle, frame.Handle);

        if (result is not (LavResult.Success or LavResult.TryAgain or LavResult.EndOfFile)) {
            result.ThrowIfError("Could not decode frame");
        }
        return result >= 0;
    }
}