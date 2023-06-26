namespace FFmpeg.Wrapper;

/// <summary> Represents a rational number (pair of numerator and denominator). </summary>
/// <remarks>
/// While rational numbers can be expressed as floating-point numbers, the
/// conversion process is a lossy one, so are floating-point operations. On the
/// other hand, the nature of FFmpeg demands highly accurate calculation of
/// timestamps. This struct serves as a generic interface for manipulating 
/// rational numbers as pairs of numerators and denominators.
/// </remarks>
public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
{
    public static Rational Zero => new(0, 1);
    public static Rational One => new(1, 1);

    public int Num { get; }
    public int Den { get; }

    public Rational(int num, int den)
    {
        Num = num;
        Den = den;
    }

    /// <summary> Returns the reciprocal of this rational, <c>1/q</c>. </summary>
    public Rational Reciprocal() => new(Den, Num);

    /// <param name="max"> Maximum allowed numerator and denominator. </param>
    public static Rational FromDouble(double value, int max) => ffmpeg.av_d2q(value, max);

    /// <summary> Rescales a fixed-point integer based on <paramref name="oldScale"/> to <paramref name="newScale"/>. </summary>
    public static long Rescale(long value, Rational oldScale, Rational newScale) => ffmpeg.av_rescale_q(value, oldScale, newScale);

    /// <summary> Rescales a timestamp based around an arbitrary time scale to a <see cref="TimeSpan"/>. </summary>
    /// <param name="scale">The scale that represents one second of time.</param>
    public static TimeSpan GetTimeSpan(long timestamp, Rational scale)
    {
        long ticks = Rescale(timestamp, scale, new Rational(1, (int)TimeSpan.TicksPerSecond));
        return TimeSpan.FromTicks(ticks);
    }

    public static Rational operator +(Rational a, Rational b) => ffmpeg.av_add_q(a, b);
    public static Rational operator -(Rational a, Rational b) => ffmpeg.av_sub_q(a, b);
    public static Rational operator *(Rational a, Rational b) => ffmpeg.av_mul_q(a, b);
    public static Rational operator /(Rational a, Rational b) => ffmpeg.av_div_q(a, b);

    //Comparison for equality is to properly handle the degenerate case of 0/0, where cmp_q() returns INT_MIN.
    public static bool operator ==(Rational a, Rational b) => a.CompareTo(b) == 0;
    public static bool operator !=(Rational a, Rational b) => a.CompareTo(b) != 0;
    public static bool operator <(Rational a, Rational b) => a.CompareTo(b) == -1;
    public static bool operator >(Rational a, Rational b) => a.CompareTo(b) == +1;
    public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) is 0 or +1;
    public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) is 0 or -1;

    public static explicit operator double(Rational q) => q.Num / (double)q.Den;

    public static implicit operator Rational(int num) => new(num, 1);
    public static implicit operator Rational(AVRational q) => new(q.num, q.den);
    public static implicit operator AVRational(Rational q) => new() { num = q.Num, den = q.Den };

    public override string ToString() => $"{Num}/{Den}";
    public override bool Equals(object other) => other is Rational r && Equals(r);
    public override int GetHashCode() => Math.Round((double)this, 10).GetHashCode();

    public bool Equals(Rational other) => other == this || ((other.Den | Den) == 0 && (other.Num ^ Num) >= 0);
    public int CompareTo(Rational other) => ffmpeg.av_cmp_q(this, other);
}