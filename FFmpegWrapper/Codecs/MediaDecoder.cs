namespace FFmpeg.Wrapper;

public abstract unsafe class MediaDecoder : CodecBase
{
    public MediaDecoder(AVCodecContext* ctx, AVMediaType expectedType, bool takeOwnership = true)
        : base(ctx, expectedType, takeOwnership) { }

    public void SendPacket(MediaPacket? pkt)
    {
        var result = (LavResult)ffmpeg.avcodec_send_packet(Handle, pkt == null ? null : pkt.Handle);

        if (result != LavResult.Success && !(result == LavResult.EndOfFile && pkt == null)) {
            result.ThrowIfError("Could not decode packet (hints: check if the decoder is open, try receiving frames first)");
        }
    }

    public bool ReceiveFrame(MediaFrame frame)
    {
        var result = (LavResult)ffmpeg.avcodec_receive_frame(Handle, frame.Handle);

        if (result is not (LavResult.Success or LavResult.TryAgain or LavResult.EndOfFile)) {
            result.ThrowIfError("Could not decode frame");
        }
        return result == 0;
    }
}