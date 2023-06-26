namespace FFmpegWrapper.Tests;

using FFmpeg.Wrapper;

public class FrameTests
{
    [Fact]
    public unsafe void Video_Props()
    {
        var frame = new VideoFrame(1280, 720, PixelFormats.RGBA);

        Assert.Equal(1280, frame.Format.Width);
        Assert.Equal(720, frame.Format.Height);
        Assert.Equal(PixelFormats.RGBA, frame.Format.PixelFormat);
        Assert.False(frame.Format.IsPlanar);

        Assert.Equal(1280, frame.GetRowSpan<uint>(0).Length);

        var pixels = frame.GetPlaneSpan<uint>(0, out int stride);
        Assert.Equal(1280 * 720, pixels.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetRowSpan<uint>(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetRowSpan<uint>(720));

        frame.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = frame.Handle);
    }

    [Fact]
    public unsafe void Audio_Props()
    {
        var frame = new AudioFrame(SampleFormats.FloatPlanar, 48000, 2, 1024);

        Assert.Equal(1024, frame.Capacity);
        Assert.Equal(1024, frame.Count);
        Assert.Equal(2, frame.NumChannels);
        Assert.Equal(48000, frame.SampleRate);
        Assert.True(frame.IsPlanar);

        Assert.Equal(1024, frame.GetSamples<float>(0).Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetSamples<float>(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetSamples<float>(2));

        frame.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = frame.Handle);
    }
}
