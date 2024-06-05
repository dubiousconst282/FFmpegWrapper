namespace FFmpegWrapper.Tests;

using FFmpeg.AutoGen;
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

    [Fact]
    public unsafe void ChannelLayout_Props()
    {
        var a = ChannelLayout.GetDefault(2);
        Assert.Equal(2, a.NumChannels);
        Assert.Equal(ChannelOrder.Native, a.Order);
        Assert.Equal("stereo", a.ToString());

        var b = ChannelLayout.FromString("FL+FC+FR");
        Assert.Equal(3, b.NumChannels);
        Assert.Equal(ChannelOrder.Custom, b.Order);
        Assert.Equal(AudioChannel.FrontLeft, b.GetChannel(0));
        Assert.Equal(AudioChannel.FrontCenter, b.GetChannel(1));
        Assert.Equal(AudioChannel.FrontRight, b.GetChannel(2));
    }

    [Fact]
    public void SideData_Integration()
    {
        using var frame = new VideoFrame(128, 128, PixelFormats.RGBA);

        Assert.Equal(0, frame.SideData.Count);

        var entry1 = frame.SideData.Add(AVFrameSideDataType.AV_FRAME_DATA_DISPLAYMATRIX, 9 * 4);
        var entry2 = frame.SideData.Add(AVFrameSideDataType.AV_FRAME_DATA_DETECTION_BBOXES, 128);
        Assert.Equal(2, frame.SideData.Count);

        Assert.Equal(9 * 4, entry1.Data.Length);
        Assert.NotNull(frame.SideData.GetDisplayMatrix());

        frame.SideData.Remove(AVFrameSideDataType.AV_FRAME_DATA_DISPLAYMATRIX);
        Assert.Equal(1, frame.SideData.Count);
    }
}
