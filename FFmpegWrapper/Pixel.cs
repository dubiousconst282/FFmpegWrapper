using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FFmpegWrapper
{
    /// <summary>Represents a 32-bit ARGB color, packed in BGRA order.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Pixel
    {
        public byte B, G, R, A;

        public Pixel(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
        public Pixel(int r, int g, int b)
        {
            R = Clampb(r);
            G = Clampb(g);
            B = Clampb(b);
            A = 255;
        }
        public Pixel(int r, int g, int b, int a)
        {
            R = Clampb(r);
            G = Clampb(g);
            B = Clampb(b);
            A = Clampb(a);
        }
        public Pixel(uint argb)
        {
            A = (byte)(argb >> 24);
            R = (byte)(argb >> 16);
            G = (byte)(argb >> 8);
            B = (byte)(argb >> 0);
        }

        public static implicit operator Pixel(int rgb) => new Pixel((uint)rgb | 0xFF000000);
        public static implicit operator Pixel(uint argb) => new Pixel(argb);

        public static implicit operator int(Pixel p) => p.A << 24 | p.R << 16 | p.G << 8 | p.B;
        public static implicit operator uint(Pixel p) => (uint)(p.A << 24 | p.R << 16 | p.G << 8 | p.B);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Clampb(int n)
        {
            return (byte)(n < 0 ? 0 : n > 255 ? 255 : n);
        }

        public override string ToString()
        {
            return $"#{R:X2}{G:X2}{B:X2} A={A * 100 / 255}%";
        }
    }
}