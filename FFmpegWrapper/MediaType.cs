using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public enum MediaType
    {
        Unknown     = AVMediaType.AVMEDIA_TYPE_UNKNOWN,
        Video       = AVMediaType.AVMEDIA_TYPE_VIDEO,
        Audio       = AVMediaType.AVMEDIA_TYPE_AUDIO,
        Subtitle    = AVMediaType.AVMEDIA_TYPE_SUBTITLE
    }
}
