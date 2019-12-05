using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public static class FFmpegExtensions
    {
        public static double ToDouble(this AVRational avr)
        {
            return ffmpeg.av_q2d(avr);
        }
        public static AVRational ToRational(this double x)
        {
            return ffmpeg.av_d2q(x, 100000);
        }
    }
}
