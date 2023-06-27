namespace FFmpegWrapper.Tests;

using FFmpeg.AutoGen;
using FFmpeg.Wrapper;

public class OptionTests
{
    [Fact]
    public void GetOptions()
    {
        var options = MediaCodec.GetEncoder("libx264").GetOptions();
        Assert.NotEmpty(options);

        var mestOpt = options.First(o => o.Name == "me_method");
        var namedOpts = mestOpt.GetNamedValues();

        Assert.Equal("me_method: int", mestOpt.ToString());
        Assert.Equal("Set motion estimation method", mestOpt.Description);
        Assert.Contains(namedOpts, v => v.Name == "hex");
        Assert.DoesNotContain(namedOpts, v => v.Type != AVOptionType.AV_OPT_TYPE_CONST);
    }
}