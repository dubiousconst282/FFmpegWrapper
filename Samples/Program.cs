using FFmpeg.AutoGen;
using FFmpegWrapper;
using FFmpegWrapper.Codec;
using FFmpegWrapper.Container;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Samples
{
    class Program
    {
        unsafe static void Main(string[] args)
        {
            FFmpegHelpers.RegisterBinaries(enableDebugLogging: false);
            new ConsoleVideoPlayer(@"E:\Torrents\The.Simpsons.S30E19.720p.WEB.x265-MiNX[eztv].mkv").Run();
        }
    }
}
