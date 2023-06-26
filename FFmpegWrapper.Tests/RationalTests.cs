namespace FFmpegWrapper.Tests;

using FFmpeg.AutoGen;
using FFmpeg.Wrapper;

public class RationalTests
{
    [Fact]
    public void Casting()
    {
        var orig = new AVRational() { num = 123, den = 456 };

        Assert.Equal(new Rational(123, 456), (Rational)orig);
        Assert.Equal(orig, (AVRational)(Rational)orig);
        Assert.Equal(new Rational(123, 1), (Rational)123);

        Assert.Equal(0.125, (double)new Rational(1, 8));
        Assert.Equal(new Rational(22, 7), Rational.FromDouble(3.14159, 100));
    }

    [Fact]
    public void Rescale()
    {
        Assert.Equal(1235, Rational.Rescale(123456, new Rational(1, 100), new Rational(1, 1)));
        Assert.Equal(TimeSpan.FromMilliseconds(123456), Rational.GetTimeSpan(123456, new Rational(1, 1000)));
    }

    [Fact]
    public void Arithmetic()
    {
        var half = new Rational(1, 2);
        var quart = new Rational(1, 4);
        var negQuart = new Rational(-1, 4);

        Assert.Equal(new Rational(1, 1), half + half);
        Assert.Equal(new Rational(0, 1), half - half);
        Assert.Equal(new Rational(1, 4), half * half);
        Assert.Equal(new Rational(2, 1), half / quart);

        Assert.Equal(new Rational(1, 4), half + negQuart);
        Assert.Equal(new Rational(3, 4), half - negQuart);
        Assert.Equal(new Rational(-1, 8), half * negQuart);
        Assert.Equal(new Rational(-2, 1), half / negQuart);
    }

    [Fact]
    public void Comparison()
    {
        Assert.Equal(0, new Rational(5, 10).CompareTo(new Rational(1, 2)));
        Assert.Equal(0, new Rational(5, 1).CompareTo(new Rational(10, 2)));

        Assert.Equal(-1, new Rational(5, 1).CompareTo(new Rational(10, 1)));
        Assert.Equal(+1, new Rational(10, 1).CompareTo(new Rational(5, 1)));

        Assert.Equal(-1, new Rational(1, 4).CompareTo(new Rational(1, 2)));
        Assert.Equal(+1, new Rational(1, 2).CompareTo(new Rational(1, 4)));

        Assert.Equal(0, new Rational(123, 0).CompareTo(new Rational(456, 0)));
        Assert.Equal(int.MinValue, new Rational(0, 0).CompareTo(new Rational(456, 0)));

        Assert.True(new Rational(0, 0).Equals(new Rational(0, 0))); // NaN
        Assert.True(new Rational(123, 0).Equals(new Rational(456, 0))); // Inf
        Assert.False(new Rational(-123, 0).Equals(new Rational(456, 0))); // -Inf
        Assert.False(new Rational(123, 0).Equals(new Rational(-456, 0))); // -Inf
    }
}