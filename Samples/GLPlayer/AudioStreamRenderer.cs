using FFmpeg.Wrapper;

public class AudioStreamRenderer : StreamRenderer
{
    private SwResampler _resampler;
    private IAudioSink _sink;
    private AudioFrame _frame = new();
    private TimeSpan _framePos;

    public AudioStreamRenderer(MediaDemuxer demuxer, MediaStream stream)
        : base(demuxer, stream)
    {
        var decoder = (AudioDecoder)_decoder;
#pragma warning disable CA1416
        _sink = new WasapiAudioSink(decoder.Format, latencyMs: 100);
#pragma warning restore
        _resampler = new SwResampler(decoder.Format, _sink.Format);
        decoder.Open();

        _sink.Start();
    }

    public override void Tick(PlayerClock refClock, ref TimeSpan tickInterval)
    {
        int maxBufferedSamples = _resampler.OutputFormat.SampleRate * 5;

        while (_resampler.BufferedSamples < maxBufferedSamples && ReceiveFrame(_frame)) {
            _resampler.SendFrame(_frame);
            _framePos = Stream.GetTimestamp(_frame.BestEffortTimestamp ?? 0);
        }

        var playBuffer = _sink.GetQueueBuffer<byte>();
        int numSamples = _resampler.ReceiveFrame(playBuffer);
        _sink.AdvanceQueue(numSamples);

        var pts = _framePos - Rational.GetTimeSpan(_resampler.BufferedSamples - numSamples, new Rational(1, _resampler.OutputFormat.SampleRate));
        Clock.SetFrameTime(pts);
    }

    public override void Flush()
    {
        base.Flush();
        _resampler.DropOutputSamples(_resampler.BufferedSamples * 2);
    }

    public override void Dispose()
    {
        base.Dispose();

        _sink.Stop();
        _sink.Dispose();
        _resampler.Dispose();
    }
}
