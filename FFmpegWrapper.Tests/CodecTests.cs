namespace FFmpegWrapper.Tests;

using FFmpeg.AutoGen;
using FFmpeg.Wrapper;

public class CodecTests
{
    [Fact]
    public void AVCodec_Props()
    {
        var codec1 = MediaCodec.GetEncoder("mpeg2video");
        var codec2 = MediaCodec.GetEncoder("libmp3lame");

        Assert.Equal("mpeg2video", codec1.Name);
        Assert.Equal(MediaTypes.Video, codec1.Type);
        Assert.Equal(AVCodecID.AV_CODEC_ID_MPEG2VIDEO, codec1.Id);
        Assert.True(codec1.IsEncoder);
        Assert.False(codec1.IsDecoder);
        Assert.Equal(0, codec1.SupportedChannelLayouts.Length);
        Assert.Equal(0, codec1.SupportedSampleFormats.Length);
        Assert.Equal(PixelFormats.YUV420P, codec1.SupportedPixelFormats[0]);
        Assert.True(codec1.SupportedFramerates.Length is > 10 and < 100);

        Assert.Equal("libmp3lame", codec2.Name);
        Assert.Equal(MediaTypes.Audio, codec2.Type);
        Assert.Equal(AVCodecID.AV_CODEC_ID_MP3, codec2.Id);
        Assert.True(codec2.IsEncoder);
        Assert.False(codec2.IsDecoder);
        Assert.Equal(2, codec2.SupportedChannelLayouts[1].nb_channels);
        Assert.Equal(SampleFormats.FloatPlanar, codec2.SupportedSampleFormats[1]);
        Assert.Equal(44100, codec2.SupportedSampleRates[0]);
        Assert.Equal(0, codec2.SupportedPixelFormats.Length);
        Assert.Equal(0, codec2.SupportedFramerates.Length);
    }

    [Fact]
    public void AVCodec_GetRegistered()
    {
        var codecs = MediaCodec.GetRegisteredCodecs().ToList();

        Assert.NotEmpty(codecs);
        Assert.Contains(codecs, c => c.Id == CodecIds.H264);
    }
}