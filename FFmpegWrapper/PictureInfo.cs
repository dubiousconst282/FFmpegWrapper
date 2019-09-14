using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public struct PictureInfo
    {
        public int Width { get; }
        public int Height { get; }
        public AVPixelFormat PixelFormat { get; }

        public PictureInfo(int w, int h)
        {
            Width = w;
            Height = h;
            PixelFormat = AVPixelFormat.AV_PIX_FMT_BGRA;
        }
        public PictureInfo(int w, int h, AVPixelFormat fmt)
        {
            Width = w;
            Height = h;
            PixelFormat = fmt;
        }

        public override string ToString()
        {
            var fmt = PixelFormat.ToString().Substring("AV_PIX_FMT_".Length);
            return $"Resolution={Width}x{Height} Format={fmt}";
        }
    }
}
