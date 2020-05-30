using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper.Container
{
    public enum ContainerType
    {
        //General
        Mp4,
        Mov,
        Matroska,
        WebM,

        //Audio
        Mp3,
        Ogg,
        Flac,
        M4a,
        Wav
    }
    public static class ContainerTypeEx
    {
        public static string GetExtension(this ContainerType container)
        {
            return container switch
            {
                ContainerType.Mp4       => "mp4",
                ContainerType.Mov       => "mov",
                ContainerType.Matroska  => "mkv",
                ContainerType.WebM      => "webm",

                ContainerType.Mp3       => "mp3",
                ContainerType.Ogg       => "ogg",
                ContainerType.Flac      => "flac",
                ContainerType.M4a       => "m4a",
                ContainerType.Wav       => "wav",
                _ => throw new ArgumentException(),
            };
        }
        public static unsafe AVOutputFormat* GetOutputFormat(this ContainerType type)
        {
            var fmt = ffmpeg.av_guess_format(null, "dummy." + type.GetExtension(), null);
            if (fmt == null) {
                throw new NotSupportedException();
            }
            return fmt;
        }
    }
}
