using FFmpeg.AutoGen;

namespace FFmpegWrapper.Codec
{
    public sealed class CodecIds
    {
        public const AVCodecID H264     = AVCodecID.AV_CODEC_ID_H264;
        public const AVCodecID HEVC     = AVCodecID.AV_CODEC_ID_HEVC;

        public const AVCodecID VP8      = AVCodecID.AV_CODEC_ID_VP8;
        public const AVCodecID VP9      = AVCodecID.AV_CODEC_ID_VP9;
        public const AVCodecID AV1      = AVCodecID.AV_CODEC_ID_AV1;

        public const AVCodecID MP3      = AVCodecID.AV_CODEC_ID_MP3;
        public const AVCodecID AAC      = AVCodecID.AV_CODEC_ID_AAC;
        public const AVCodecID AC3      = AVCodecID.AV_CODEC_ID_AC3;

        public const AVCodecID FLAC     = AVCodecID.AV_CODEC_ID_FLAC;

        public const AVCodecID Vorbis   = AVCodecID.AV_CODEC_ID_VORBIS;
        public const AVCodecID Opus     = AVCodecID.AV_CODEC_ID_OPUS;
    }
}
