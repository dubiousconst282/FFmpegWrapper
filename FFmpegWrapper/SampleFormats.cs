using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public sealed class SampleFormats
    {
        /// <summary> signed 16 bits </summary>
        public const AVSampleFormat S16         = AVSampleFormat.AV_SAMPLE_FMT_S16;

        /// <summary> signed 16 bits, planar </summary>
        public const AVSampleFormat S16Planar   = AVSampleFormat.AV_SAMPLE_FMT_S16P;

        /// <summary> 32 bits floating point </summary>
        public const AVSampleFormat Float       = AVSampleFormat.AV_SAMPLE_FMT_FLT;

        /// <summary> 32 bits floating point, planar </summary>
        public const AVSampleFormat FloatPlanar = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
    }
}
