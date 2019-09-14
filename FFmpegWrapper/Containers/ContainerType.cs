using System;

namespace FFmpegWrapper.Container
{
    public enum ContainerType
    {
        //General
        Mp4,
        Matroska,
        WebM,
        Mov,

        //Audio
        Mp3,
        Ogg,
        Flac,
        Wav
    }
    public static class ContainerTypeEx
    {
        public static string GetExtension(this ContainerType container)
        {
            switch (container) {
                case ContainerType.Mp4:        return "mp4";
                case ContainerType.Matroska:   return "mkv";
                case ContainerType.WebM:       return "webm";
                case ContainerType.Mov:        return "mov";

                case ContainerType.Mp3:        return "mp3";
                case ContainerType.Ogg:        return "ogg";
                case ContainerType.Flac:       return "flac";
                case ContainerType.Wav:        return "wav";
                default: throw new ArgumentException();
            }
        }
    }
}
