global using Xunit;

using System.Runtime.CompilerServices;

using FFmpeg.AutoGen;

internal class Globals
{
    [ModuleInitializer]
    public static void ModuleInit()
    {
        //Try get shared ffmpeg binary directory from %PATH%
        if (OperatingSystem.IsWindows()) {
            foreach (string dir in Environment.GetEnvironmentVariable("PATH")!.Split(';')) {
                if (File.Exists(dir + "/ffmpeg.exe")) {
                    ffmpeg.RootPath = dir;
                    break;
                }
            }
        }
    }
}