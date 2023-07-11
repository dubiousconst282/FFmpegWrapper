namespace FFmpeg.Wrapper;

public readonly struct PictureFormat : IEquatable<PictureFormat>
{
    public int Width { get; }
    public int Height { get; }
    public AVPixelFormat PixelFormat { get; }

    /// <summary> Pixel aspect ratio, <c>width / height</c>. </summary>
    /// <remarks> May be <c>0/1</c> if unknown or undefined. </remarks>
    public Rational PixelAspectRatio { get; }

    public int NumPlanes => ffmpeg.av_pix_fmt_count_planes(PixelFormat);
    public bool IsPlanar => NumPlanes >= 2;

    public PictureFormat(int width, int height, AVPixelFormat pixelFormat)
    {
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        PixelAspectRatio = Rational.Zero;
    }
    public PictureFormat(int width, int height, AVPixelFormat pixelFormat, Rational pixelAspectRatio)
    {
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        PixelAspectRatio = pixelAspectRatio;
    }

    /// <param name="align">Ensures that width and height are a multiple of this value.</param>
    public PictureFormat GetScaled(int newWidth, int newHeight, AVPixelFormat newFormat = PixelFormats.None, bool keepAspectRatio = true, int align = 1)
    {
        if (keepAspectRatio) {
            double scale = Math.Min(newWidth / (double)Width, newHeight / (double)Height);
            newWidth = (int)Math.Round(Width * scale);
            newHeight = (int)Math.Round(Height * scale);
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
        return $"{Width}x{Height} {ffmpeg.av_get_pix_fmt_name(PixelFormat)}";
    }

    public bool Equals(PictureFormat other) =>
        other.Width == Width && other.Height == Height && 
        other.PixelFormat == PixelFormat &&
        other.PixelAspectRatio.Equals(PixelAspectRatio);

    public override bool Equals(object obj) => obj is PictureFormat other && Equals(other);
    public override int GetHashCode() => (Width, Height, (int)PixelFormat).GetHashCode();
}

/// <summary> Contains colorspace information. https://trac.ffmpeg.org/wiki/colorspace </summary>
public readonly struct PictureColorspace
{
    public AVColorSpace Matrix { get; }
    public AVColorPrimaries Primaries { get; }
    public AVColorTransferCharacteristic Transfer { get; }
    public AVColorRange Range { get; }

    public PictureColorspace(AVColorSpace matrix, AVColorPrimaries primaries, AVColorTransferCharacteristic trc, AVColorRange range)
    {
        Matrix = matrix;
        Primaries = primaries;
        Transfer = trc;
        Range = range;
    }

    //TODO: Expose stuff from https://github.com/FFmpeg/FFmpeg/blob/master/libavutil/csp.h
    //      This colorspace stuff seems like a nightmare to deal with, and there's still crap like HDR+ to make it even worse.

    public override string ToString()
    {
        return $"{ffmpeg.av_color_range_name(Range)}, {ffmpeg.av_color_space_name(Matrix)}/{ffmpeg.av_color_primaries_name(Primaries)}/{ffmpeg.av_color_transfer_name(Transfer)}";
    }
}