namespace FFmpeg.Wrapper;

public class HardwareFrameConstraints
{
    public AVPixelFormat[] ValidHardwareFormats { get; }
    public AVPixelFormat[] ValidSoftwareFormats { get; }

    public int MinWidth { get; }
    public int MinHeight { get; }

    public int MaxWidth { get; }
    public int MaxHeight { get; }

    public unsafe HardwareFrameConstraints(AVHWFramesConstraints* desc)
    {
        ValidHardwareFormats = Helpers.GetSpanFromSentinelTerminatedPtr(desc->valid_hw_formats, PixelFormats.None).ToArray();
        ValidSoftwareFormats = Helpers.GetSpanFromSentinelTerminatedPtr(desc->valid_sw_formats, PixelFormats.None).ToArray();
        MinWidth = desc->min_width;
        MinHeight = desc->min_height;
        MaxWidth = desc->max_width;
        MaxHeight = desc->max_height;
    }

    public bool IsValidDimensions(int width, int height)
    {
        return width >= MinWidth && width <= MaxWidth &&
               height >= MinHeight && height <= MaxHeight;
    }
    public bool IsValidFormat(in PictureFormat format)
    {
        return IsValidDimensions(format.Width, format.Height) && 
               Array.IndexOf(ValidSoftwareFormats, format.PixelFormat) >= 0;
    }
}