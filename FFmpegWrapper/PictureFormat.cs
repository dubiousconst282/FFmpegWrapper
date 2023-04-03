namespace FFmpeg.Wrapper;

public readonly struct PictureFormat
{
    public int Width { get; }
    public int Height { get; }
    public AVPixelFormat PixelFormat { get; }

    public int NumPlanes => ffmpeg.av_pix_fmt_count_planes(PixelFormat);
    public bool IsPlanar => NumPlanes >= 2;

    public PictureFormat(int w, int h, AVPixelFormat fmt = AVPixelFormat.AV_PIX_FMT_RGBA)
    {
        Width = w;
        Height = h;
        PixelFormat = fmt;
    }

    public override string ToString()
    {
        var fmt = PixelFormat.ToString().Substring("AV_PIX_FMT_".Length);
        return $"{Width}x{Height} {fmt}";
    }
}
