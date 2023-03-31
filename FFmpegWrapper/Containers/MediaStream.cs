namespace FFmpeg.Wrapper;

public unsafe class MediaStream
{
    public AVStream* Handle { get; }

    public int Index => Handle->index;

    public AVMediaType Type => Handle->codecpar->codec_type;

    /// <summary> The fundamental unit of time (in seconds) in terms of which frame timestamps are represented. </summary>
    public AVRational TimeBase => Handle->time_base;

    /// <inheritdoc cref="TimeBase"/>
    public double TimeScale => ffmpeg.av_q2d(Handle->time_base);

    /// <summary> Pts of the first frame of the stream in presentation order, in stream time base. </summary>
    public long? StartTime => Helpers.GetTimestamp(Handle->start_time);

    /// <summary> Decoding: duration of the stream, in stream time base. If a source file does not specify a duration, but does specify a bitrate, this value will be estimated from bitrate and file size. </summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(Handle->duration * TimeScale);

    public MediaStream(AVStream* stream)
    {
        Handle = stream;
    }
}