using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public abstract unsafe class MediaEncoder : CodecBase
    {
        public int BitRate
        {
            get => (int)Context->bit_rate;
            set => Context->bit_rate = value;
        }

        public MediaEncoder(AVCodecID codec, AVMediaType parentType)
            : base(FindEncoder(codec, parentType))
        {
        }

        private static AVCodec* FindEncoder(AVCodecID codecId, AVMediaType type)
        {
            AVCodec* codec = ffmpeg.avcodec_find_encoder(codecId);
            if (codec == null) {
                throw new NotSupportedException($"Could not find encoder for codec {codecId.ToString().Substring("AV_CODEC_ID_".Length)}.");
            }
            if (codec->type != type) {
                throw new ArgumentException("Codec is not valid for the media type.");
            }
            return codec;
        }

        public void SetOption(string name, string value)
        {
            ffmpeg.av_opt_set(Context->priv_data, name, value, 0);
        }

        public LavResult SendFrame(AVFrame* frame, bool throwOnError = true)
        {
            int ret = ffmpeg.avcodec_send_frame(Context, frame);
            if (throwOnError) ret.CheckError("Could not encode frame");
            return (LavResult)ret;
        }
        public LavResult ReceivePacket(AVPacket* pkt)
        {
            return (LavResult)ffmpeg.avcodec_receive_packet(Context, pkt);
        }

        public LavResult ReceivePacket(MediaPacket packet)
        {
            var pkt = new AVPacket();

            try {
                var result = ReceivePacket(&pkt);
                if (result.IsSuccess()) {
                    packet.SetData(&pkt);
                }
                return result;
            } finally {
                ffmpeg.av_packet_unref(&pkt);
            }
        }
    }
}
