namespace FrameExtractor;

using System.Runtime.InteropServices;

using FFmpeg.AutoGen;
using FFmpeg.Wrapper;

internal static unsafe class AutoRotateUtils
{
    public static AVPacketSideData* GetPacketSideData(this MediaPacket packet, AVPacketSideDataType type)
    {
        return ffmpeg.av_packet_side_data_get(packet.Handle->side_data, packet.Handle->side_data_elems, type);
    }

    public static AVPacketSideData* GetPacketSideData(this MediaCodecParameters codecPars, AVPacketSideDataType type)
    {
        return ffmpeg.av_packet_side_data_get(codecPars.Handle->coded_side_data, codecPars.Handle->nb_coded_side_data, type);
    }

    public static int[]? GetDisplayMatrix(this MediaCodecParameters codecPars)
    {
        var sideData = codecPars.GetPacketSideData(AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX);
        if (sideData == null || sideData->size != 4 * 9) return null; // 3*3 int32 matrix
        return MemoryMarshal.Cast<byte, int>(new ReadOnlySpan<byte>(sideData->data, (int)sideData->size)).ToArray();
    }

    /// <summary>
    /// Get clockwise 0-359 degree rotation from display matrix.
    /// </summary>
    public static int GetDisplayRotation(int[]? displayMatrix)
    {
        if (displayMatrix is null) return 0;
        if (displayMatrix.Length != 9) throw new ArgumentOutOfRangeException(nameof(displayMatrix), "display matrix'length must be 9=3*3");
        var arr = new int_array9();
        arr.UpdateFrom(displayMatrix);
        var angle = ffmpeg.av_display_rotation_get(in arr); // counterclockwise. in range [-180.0, 180.0]
        if (double.IsNaN(angle)) return 0;

        // https://github.com/FFmpeg/FFmpeg/blob/cdcb4b98b7f74d87a6274899ff70724795d551cb/fftools/cmdutils.c#L1107 
        angle = -Math.Round(angle); // clockwise
        angle -= 360 * Math.Floor((angle / 360) + (0.9 / 360)); // clamp to [0, 360)
        return (int)angle;
    }

    public static string? GetAutoRotateFilterDescription(this MediaCodecParameters codecPars)
    {
        // https://github.com/FFmpeg/FFmpeg/blob/7b47099bc080ee597327476c0df44d527c349862/fftools/ffmpeg_filter.c#L1711
        var displayMatrix = codecPars.GetDisplayMatrix();
        if (displayMatrix is null) return null;
        var theta = GetDisplayRotation(displayMatrix);
        string desc = string.Empty;
        if (theta == 90) {
            desc = displayMatrix[3] > 0 ? "transpose=cclock_flip" : "transpose=clock";
        } else if (theta == 180) {
            if (displayMatrix[0] < 0) {
                desc += "hflip";
            }
            if (displayMatrix[4] < 0) {
                desc += desc.Length > 0 ? ",vflip" : "vflip";
            }
        } else if (theta == 270) {
            desc = displayMatrix[3] < 0 ? "transpose=clock_flip" : "transpose=cclock";
        } else if (theta == 0) {
            if (displayMatrix[4] < 0) {
                desc = "vflip";
            }
        }
        return desc.Length == 0 ? null : desc;
    }
}
