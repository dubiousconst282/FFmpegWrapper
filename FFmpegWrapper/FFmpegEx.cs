using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    internal unsafe class ffmpegex
    {
        //https://raw.githubusercontent.com/Ruslan-B/FFmpeg.AutoGen/master/FFmpeg.AutoGen/FFmpeg.functions.export.g.cs

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate void av_image_copy_delegate(byte** dst_data, int* dst_linesizes, byte** src_data, int* src_linesizes, AVPixelFormat pix_fmt, int width, int height);
        private static av_image_copy_delegate av_image_copy_fptr = (byte** dst_data, int* dst_linesizes, byte** src_data, int* src_linesizes, AVPixelFormat pix_fmt, int width, int height) => {
            av_image_copy_fptr = ffmpeg.GetFunctionDelegate<av_image_copy_delegate>(ffmpeg.GetOrLoadLibrary("avutil"), "av_image_copy");
            if (av_image_copy_fptr == null) {
                av_image_copy_fptr = delegate {
                    throw new PlatformNotSupportedException("av_image_copy is not supported on this platform.");
                };
            }
            av_image_copy_fptr(dst_data, dst_linesizes, src_data, src_linesizes, pix_fmt, width, height);
        };
        /// <summary>Copy image in src_data to dst_data.</summary>
        /// <param name="dst_linesizes">linesizes for the image in dst_data</param>
        /// <param name="src_linesizes">linesizes for the image in src_data</param>
        public static void av_image_copy(byte** dst_data, int* dst_linesizes, byte** src_data, int* src_linesizes, AVPixelFormat pix_fmt, int width, int height)
        {
            av_image_copy_fptr(dst_data, dst_linesizes, src_data, src_linesizes, pix_fmt, width, height);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate int sws_scale_delegate(SwsContext* @c, byte** @srcSlice, int* @srcStride, int @srcSliceY, int @srcSliceH, byte** @dst, int* @dstStride);
        private static sws_scale_delegate sws_scale_fptr = (SwsContext* @c, byte** @srcSlice, int* @srcStride, int @srcSliceY, int @srcSliceH, byte** @dst, int* @dstStride) => {
            sws_scale_fptr = ffmpeg.GetFunctionDelegate<sws_scale_delegate>(ffmpeg.GetOrLoadLibrary("swscale"), "sws_scale");
            if (sws_scale_fptr == null) {
                sws_scale_fptr = delegate {
                    throw new PlatformNotSupportedException("sws_scale is not supported on this platform.");
                };
            }
            return sws_scale_fptr(@c, @srcSlice, @srcStride, @srcSliceY, @srcSliceH, @dst, @dstStride);
        };
        /// <summary>Scale the image slice in srcSlice and put the resulting scaled slice in the image in dst. A slice is a sequence of consecutive rows in an image.</summary>
        /// <param name="c">the scaling context previously created with sws_getContext()</param>
        /// <param name="srcSlice">the array containing the pointers to the planes of the source slice</param>
        /// <param name="srcStride">the array containing the strides for each plane of the source image</param>
        /// <param name="srcSliceY">the position in the source image of the slice to process, that is the number (counted starting from zero) in the image of the first row of the slice</param>
        /// <param name="srcSliceH">the height of the source slice, that is the number of rows in the slice</param>
        /// <param name="dst">the array containing the pointers to the planes of the destination image</param>
        /// <param name="dstStride">the array containing the strides for each plane of the destination image</param>
        /// <returns>the height of the output slice</returns>
        public static int sws_scale(SwsContext* @c, byte** @srcSlice, int* @srcStride, int @srcSliceY, int @srcSliceH, byte** @dst, int* @dstStride)
        {
            return sws_scale_fptr(@c, @srcSlice, @srcStride, @srcSliceY, @srcSliceH, @dst, @dstStride);
        }

        public static int av_opt_set_list<T>(void* obj, string name, T[] val, int search_flags) where T : unmanaged
        {
            fixed (T* pVal = val) {
                return ffmpeg.av_opt_set_bin(obj, name, (byte*)pVal, val.Length * sizeof(T), search_flags);
            }
        }
    }
}
