namespace FFmpeg.Wrapper;

public unsafe class MediaStream
{
    public AVStream* Handle { get; }

    public int Index => Handle->index;

    public AVMediaType Type => Handle->codecpar->codec_type;

    /// <inheritdoc cref="AVStream.time_base" />
    public Rational TimeBase => Handle->time_base;

    /// <summary> Pts of the first frame of the stream in presentation order, in stream time base. </summary>
    public long? StartTime => Helpers.GetPTS(Handle->start_time);

    /// <inheritdoc cref="AVStream.duration" />
    public TimeSpan? Duration => Helpers.GetTimeSpan(Handle->duration, TimeBase);

    /// <inheritdoc cref="AVStream.avg_frame_rate" />
    public Rational AvgFrameRate => Handle->avg_frame_rate;

    /// <inheritdoc cref="AVStream.r_frame_rate" />
    public Rational RealFrameRate => Handle->r_frame_rate;

    public MediaDictionary Metadata => new(&Handle->metadata);

    /// <inheritdoc cref="AVStream.disposition" />
    public MediaStreamDisposition Disposition => (MediaStreamDisposition)Handle->disposition;

    /// <inheritdoc cref="AVStream.codecpar" />
    public MediaCodecParameters CodecPars => new(Handle->codecpar);

    public MediaStream(AVStream* stream)
    {
        Handle = stream;
    }

    /// <summary> Returns the corresponding <see cref="TimeSpan"/> for the given timestamp based on <see cref="TimeBase"/> units. </summary>
    public TimeSpan GetTimestamp(long pts) => Rational.GetTimeSpan(pts, TimeBase);
}

[Flags]
public enum MediaStreamDisposition
{
    None,
    /// <summary> The stream should be chosen by default among other streams of the same type, unless the user has explicitly specified otherwise. </summary>
    Default = ffmpeg.AV_DISPOSITION_DEFAULT,
    /// <summary> The stream is not in original language. </summary>
    /// <remarks>
    /// <see cref="Original"/> is the inverse of this disposition. At most one of them should be set in properly tagged streams. <br/>
    /// This disposition may apply to any stream type, not just audio.
    /// </remarks>
    Dub = ffmpeg.AV_DISPOSITION_DUB,
    /// <summary> The stream is in original language. </summary>
    Original = ffmpeg.AV_DISPOSITION_ORIGINAL,
    /// <summary> The stream is a commentary track. </summary>
    Comment = ffmpeg.AV_DISPOSITION_COMMENT,
    /// <summary> The stream contains song lyrics. </summary>
    Lyrics = ffmpeg.AV_DISPOSITION_LYRICS,
    /// <summary> The stream contains karaoke audio. </summary>
    Karaoke = ffmpeg.AV_DISPOSITION_KARAOKE,
    /// <summary> Track should be used during playback by default. Useful for subtitle track that should be displayed even when user did not explicitly ask for subtitles. </summary>
    Forced = ffmpeg.AV_DISPOSITION_FORCED,
    /// <summary> The stream is intended for hearing impaired audiences. </summary>
    HearingImpaired = ffmpeg.AV_DISPOSITION_HEARING_IMPAIRED,
    /// <summary> The stream is intended for visually impaired audiences. </summary>
    VisualImpaired = ffmpeg.AV_DISPOSITION_VISUAL_IMPAIRED,
    /// <summary> The audio stream contains music and sound effects without voice. </summary>
    CleanEffects = ffmpeg.AV_DISPOSITION_CLEAN_EFFECTS,
    /// <summary>
    /// The stream is stored in the file as an attached picture/"cover art" (e.g. APIC frame in ID3v2). 
    /// The first (usually only) packet associated with it will be returned among the first few packets
    /// read from the file unless seeking takes place. It can also be accessed at any time in <see cref="AVStream.attached_pic"/>.
    /// </summary>
    AttachedPic = ffmpeg.AV_DISPOSITION_ATTACHED_PIC,
    /// <summary> The stream is sparse, and contains thumbnail images, often corresponding to chapter markers. </summary>
    /// <remarks> Only ever used with <see cref="AttachedPic"/>. </remarks>
    TimedThumbnails = ffmpeg.AV_DISPOSITION_TIMED_THUMBNAILS,
    /// <summary>
    /// The stream is intended to be mixed with a spatial audio track. For example, it could be used for narration 
    /// or stereo music, and may remain unchanged by listener head rotation.
    /// </summary>
    NonDiegetic = ffmpeg.AV_DISPOSITION_NON_DIEGETIC,
    /// <summary>
    /// The subtitle stream contains captions, providing a transcription and possibly a translation of audio.
    /// Typically intended for hearing-impaired audiences.
    /// </summary>
    Captions = ffmpeg.AV_DISPOSITION_CAPTIONS,
    /// <summary>
    /// The subtitle stream contains a textual description of the video content.
    /// Typically intended for visually-impaired audiences or for the cases where the video cannot be seen.
    /// </summary>
    Descriptions = ffmpeg.AV_DISPOSITION_DESCRIPTIONS,
    /// <summary> The subtitle stream contains time-aligned metadata that is not intended to be directly presented to the user.  </summary>
    Metadata = ffmpeg.AV_DISPOSITION_METADATA,
    /// <summary> The audio stream is intended to be mixed with another stream before presentation. Corresponds to mix_type = 0 in mpegts. </summary>
    Dependent = ffmpeg.AV_DISPOSITION_DEPENDENT,
    /// <summary> The video stream contains still images. </summary>
    StillImage = ffmpeg.AV_DISPOSITION_STILL_IMAGE,
}