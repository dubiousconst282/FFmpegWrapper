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

    /// <param name="align">Ensures that width and height are a multiple of this value.</param>
    public PictureFormat GetScaled(int newWidth, int newHeight, AVPixelFormat newFormat = PixelFormats.None, bool keepAspectRatio = true, int align = 1)
    {
        if (keepAspectRatio) {
            float scale = MathF.Min(newWidth / (float)Width, newHeight / (float)Height);
            newWidth = (int)MathF.Round(Width * scale);
            newHeight = (int)MathF.Round(Height * scale);
        }
        if (newFormat == PixelFormats.None) {
            newFormat = PixelFormat;
        }
        if (align > 1) {
            newWidth = (newWidth + align - 1) / align * align;
            newHeight = (newHeight + align - 1) / align * align;
        }
        return new PictureFormat(newWidth, newHeight, newFormat);
    }

    public override string ToString()
    {
        var fmt = PixelFormat.ToString().Substring("AV_PIX_FMT_".Length);
        return $"{Width}x{Height} {fmt}";
    }
}
