using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public sealed class PixelFormats
    {
        /// <summary>planar YUV 4:2:0, 12bpp, (1 Cr &amp; Cb sample per 2x2 Y samples)</summary>
        public const AVPixelFormat YUV420P = AVPixelFormat.AV_PIX_FMT_YUV420P;
        /// <summary>planar YUV 4:2:2, 16bpp, (1 Cr &amp; Cb sample per 2x1 Y samples)</summary>
        public const AVPixelFormat YUV422P = AVPixelFormat.AV_PIX_FMT_YUV422P;
        /// <summary>planar YUV 4:4:4, 24bpp, (1 Cr &amp; Cb sample per 1x1 Y samples)</summary>
        public const AVPixelFormat YUV444P = AVPixelFormat.AV_PIX_FMT_YUV444P;
        /// <summary>planar YUV 4:1:0, 9bpp, (1 Cr &amp; Cb sample per 4x4 Y samples)</summary>
        public const AVPixelFormat YUV410P = AVPixelFormat.AV_PIX_FMT_YUV410P;
        /// <summary>planar YUV 4:1:1, 12bpp, (1 Cr &amp; Cb sample per 4x1 Y samples)</summary>
        public const AVPixelFormat YUV411P = AVPixelFormat.AV_PIX_FMT_YUV411P;

        /// <summary>packed RGB 8:8:8, 24bpp, RGBRGB...</summary>
        public const AVPixelFormat RGB24 = AVPixelFormat.AV_PIX_FMT_RGB24;
        /// <summary>packed RGB 8:8:8, 24bpp, BGRBGR...</summary>
        public const AVPixelFormat BGR24 = AVPixelFormat.AV_PIX_FMT_BGR24;

        /// <summary>packed ARGB 8:8:8:8, 32bpp, ARGBARGB...</summary>
        public const AVPixelFormat ARGB = AVPixelFormat.AV_PIX_FMT_ARGB;
        /// <summary>packed RGBA 8:8:8:8, 32bpp, RGBARGBA...</summary>
        public const AVPixelFormat RGBA = AVPixelFormat.AV_PIX_FMT_RGBA;
        /// <summary>packed ABGR 8:8:8:8, 32bpp, ABGRABGR...</summary>
        public const AVPixelFormat ABGR = AVPixelFormat.AV_PIX_FMT_ABGR;
        /// <summary>packed BGRA 8:8:8:8, 32bpp, BGRABGRA...</summary>
        public const AVPixelFormat BGRA = AVPixelFormat.AV_PIX_FMT_BGRA;

        /// <summary>packed RGB 8:8:8, 32bpp, XRGBXRGB... X=unused/undefined</summary>
        public const AVPixelFormat XRGB = AVPixelFormat.AV_PIX_FMT_0RGB;
        /// <summary>packed RGB 8:8:8, 32bpp, RGBXRGBX... X=unused/undefined</summary>
        public const AVPixelFormat RGBX = AVPixelFormat.AV_PIX_FMT_RGB0;
        /// <summary>packed BGR 8:8:8, 32bpp, XBGRXBGR... X=unused/undefined</summary>
        public const AVPixelFormat XBGR = AVPixelFormat.AV_PIX_FMT_0BGR;
        /// <summary>packed BGR 8:8:8, 32bpp, BGRXBGRX... X=unused/undefined</summary>
        public const AVPixelFormat BGRX = AVPixelFormat.AV_PIX_FMT_BGR0;

        /// <summary>Y, 8bpp</summary>
        public const AVPixelFormat Gray8 = AVPixelFormat.AV_PIX_FMT_GRAY8;
    }
}