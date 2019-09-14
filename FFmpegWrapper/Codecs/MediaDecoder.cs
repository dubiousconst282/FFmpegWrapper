using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public abstract unsafe class MediaDecoder : CodecBase
    {
        public MediaDecoder(AVCodecID codec, AVMediaType parentType)
            : base(FindDecoder(codec, parentType))
        {
        }
        public MediaDecoder(AVCodecContext* ctx, AVMediaType parentType) : base(ctx)
        {
            if (Codec->type != parentType) {
                throw new ArgumentException("Codec is not valid for the media type.");
            }
        }

        private static AVCodec* FindDecoder(AVCodecID codecId, AVMediaType type)
        {
            AVCodec* codec = ffmpeg.avcodec_find_decoder(codecId);
            if (codec == null) {
                throw new NotSupportedException($"Could not find encoder for codec {codecId.ToString().Substring("AV_CODEC_ID_".Length)}.");
            }
            if (codec->type != type) {
                throw new ArgumentException("Codec is not valid for the media type.");
            }
            return codec;
        }

        public LavResult SendPacket(AVPacket* pkt, bool throwOnError = true)
        {
            int ret = ffmpeg.avcodec_send_packet(Context, pkt);
            if (throwOnError) ret.CheckError("Could not decode packet");
            return (LavResult)ret;
        }
        public LavResult SendPacket(MediaPacket pkt, bool throwOnError = true)
        {
            var mem = pkt.Data;
            fixed (byte* pData = mem.Span) {
                AVPacket* p = stackalloc AVPacket[1];
                p->size = mem.Length;
                p->data = pData;

                p->pts = pkt.PresentationTimestamp ?? ffmpeg.AV_NOPTS_VALUE;
                p->dts = pkt.DecompressionTimestamp ?? ffmpeg.AV_NOPTS_VALUE;
                p->duration = pkt.Duration;

                return SendPacket(p, throwOnError);
            }
        }

        public LavResult ReceiveFrame(AVFrame* frame)
        {
            return (LavResult)ffmpeg.avcodec_receive_frame(Context, frame);
        }
    }
}
