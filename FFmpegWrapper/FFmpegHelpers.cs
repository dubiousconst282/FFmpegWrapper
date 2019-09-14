using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public static unsafe class FFmpegHelpers
    {
        private static readonly string Arch = Environment.Is64BitProcess ? "x64" : "x86";

        private static readonly string[] BinariesDirectories = {
            $"ffmpeg/{Arch}/",
            $"lib/{Arch}/ffmpeg/",
            $"bin/{Arch}/ffmpeg/",
            //?
        };
        public static void RegisterBinaries(string binariesDirectory = null, bool enableDebugLogging = false)
        {
            binariesDirectory = binariesDirectory ?? BinariesDirectories.FirstOrDefault(Directory.Exists);
            if (binariesDirectory != null) {
                ffmpeg.RootPath = binariesDirectory;
            }

            if (enableDebugLogging) {
                ffmpeg.av_log_set_level(ffmpeg.AV_LOG_DEBUG);
            }
        }

        public static unsafe string ErrorString(int errno)
        {
            byte* buf = stackalloc byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE + 1];
            ffmpeg.av_strerror(errno, buf, ffmpeg.AV_ERROR_MAX_STRING_SIZE);
            return Marshal.PtrToStringAnsi((IntPtr)buf);
        }
        internal static int CheckError(this int errno)
        {
            if (errno < 0 && errno != -11 /* EAGAIN */ && errno != ffmpeg.AVERROR_EOF) {
                throw new InvalidOperationException(ErrorString(errno));
            }
            return errno;
        }
        internal static int CheckError(this int errno, string msg)
        {
            if (errno < 0 && errno != -11 /* EAGAIN */ && errno != ffmpeg.AVERROR_EOF) {
                throw new InvalidOperationException(msg + ": " + ErrorString(errno));
            }
            return errno;
        }
        internal static void ThrowError(this int errno, string msg)
        {
            throw new InvalidOperationException(msg + ": " + ErrorString(errno));
        }

        internal static T[] ToArray<T>(T* p, T terminator) where T : unmanaged
        {
            if (p == null) return new T[0];
            var list = new List<T>();

            while (p != null && !terminator.Equals(*p)) {
                list.Add(*p++);
            }
            return list.ToArray();
        }
    }
}
