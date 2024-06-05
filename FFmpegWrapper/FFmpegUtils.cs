namespace FFmpeg.Wrapper;

using System.Text;

public static unsafe class FFmpegUtils
{
    /// <summary> Set log level and message callback. </summary>
    /// <param name="cb"> Callback that will receive log messages. If null, will default printing to stdout. </param> 
    public static void SetLoggerCallback(FFmpegLogLevel minLevel, Action<FFmpegLogLevel, string>? cb = null)
    {
        var buffer = new byte[16384];
        int printPrefix = 1;

        s_LogCallback = cb != null ? NativeCb : ffmpeg.av_log_default_callback;
        ffmpeg.av_log_set_level((int)minLevel);
        ffmpeg.av_log_set_callback(s_LogCallback);

        void NativeCb(void* avcl, int level, string fmt, byte* vl)
        {
            if (level > (int)minLevel) return;

            lock (cb) {
                int length;

                fixed (byte* pBuffer = buffer) {
                    int localPrintPrefix = printPrefix;
                    length = ffmpeg.av_log_format_line2(avcl, level, fmt, vl, pBuffer, buffer.Length, &localPrintPrefix);
                    length = Math.Min(length, buffer.Length - 1);
                    printPrefix = localPrintPrefix;
                }

                if (length > 0 && buffer[length - 1] == '\n') length--;
                cb((FFmpegLogLevel)level, Encoding.UTF8.GetString(buffer, 0, length));
            }
        }
    }

    static av_log_set_callback_callback? s_LogCallback;
}

public enum FFmpegLogLevel
{
    /// <summary> Print no output. </summary>
    Quiet = ffmpeg.AV_LOG_QUIET,

    /// <summary> Something went really wrong and we will crash now. </summary>
    Panic = ffmpeg.AV_LOG_PANIC,

    /// <summary> Something went wrong and recovery is not possible.
    /// For example, no header was found for a format which depends
    /// on headers or an illegal combination of parameters is used.
    /// </summary>
    Fatal = ffmpeg.AV_LOG_FATAL,

    /// <summary> Something went wrong and cannot losslessly be recovered.
    /// However, not all future data is affected.
    /// </summary>
    Error = ffmpeg.AV_LOG_ERROR,

    /// <summary> Something somehow does not look correct. This may or may not
    /// lead to problems. An example would be the use of '-vstrict -2'.
    /// </summary>
    Warning = ffmpeg.AV_LOG_WARNING,

    /// <summary> Standard information. </summary>
    Info = ffmpeg.AV_LOG_INFO,

    /// <summary> Detailed information. </summary>
    Verbose = ffmpeg.AV_LOG_VERBOSE,

    /// <summary> Stuff which is only useful for libav* developers. </summary>
    Debug = ffmpeg.AV_LOG_DEBUG,

    /// <summary> Extremely verbose debugging, useful for libav* development. </summary> 
    Trace = ffmpeg.AV_LOG_TRACE,
}