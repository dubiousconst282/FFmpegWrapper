using System;
using FFmpeg.AutoGen;
using FFmpegWrapper.Codec;

namespace FFmpegWrapper.Container
{
    public unsafe class MediaStream
    {
        public AVStream* Stream { get; }

        public int Index => Stream->index;

        public MediaType Type => (MediaType)Stream->codecpar->codec_type;

        /// <summary> Timestamp scale in seconds. </summary>
        public double TimeScale => ffmpeg.av_q2d(Stream->time_base);

        /// <summary> </summary>
        public AVRational TimeBase => Stream->time_base;

        /// <summary> Pts of the first frame of the stream in presentation order, in stream time base. </summary>
        public long? StartTime
        {
            get {
                long st = Stream->start_time;
                return st == ffmpeg.AV_NOPTS_VALUE ? (long?)null : st;
            }
        }

        public CodecBase Codec { get; private set; }

        public MediaStreamMode Mode { get; }

        public MediaStream(AVStream* stream, MediaStreamMode mode, CodecBase codec = null)
        {
            Stream = stream;
            Mode = mode;
            Codec = codec;
        }

        /// <summary> Creates and open the decoder. </summary>
        public MediaDecoder OpenDecoder()
        {
            if (Mode != MediaStreamMode.Decode) {
                throw new InvalidOperationException("Stream must be in decoding mode.");
            }

            if (Codec == null) {
                var codecId = Stream->codecpar->codec_id;
                Codec = Type switch
                {
                    MediaType.Audio => new AudioDecoder(codecId),
                    MediaType.Video => new VideoDecoder(codecId),
                    _ => throw new NotSupportedException($"Stream type {Type} is not supported."),
                };
                ffmpeg.avcodec_parameters_to_context(Codec.Context, Stream->codecpar).CheckError("Could not copy the stream parameters to the decoder.");
                Codec.Open();
            }

            return (MediaDecoder)Codec;
        }
    }
    public enum MediaStreamMode
    {
        Decode,
        Encode
    }
}
