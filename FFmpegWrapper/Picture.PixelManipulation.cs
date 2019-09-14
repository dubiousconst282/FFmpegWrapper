using FFmpeg.AutoGen;
using System;

namespace FFmpegWrapper
{
    public unsafe partial class Picture : IDisposable
    {
        public Pixel GetPixel(int x, int y)
        {
            var frame = Frame;
            if (x >= 0 && y >= 0 && x < frame->width && y < frame->height) {
                byte** p = frame->extended_data;
                int* s = (int*)&frame->linesize;
                byte* p0 = p[0];
                int r4 = x * 4 + y * s[0];
                switch (Format) {
                    case AVPixelFormat.AV_PIX_FMT_ARGB: return new Pixel(a: p0[r4 + 0], r: p0[r4 + 1], g: p0[r4 + 2], b: p0[r4 + 3]);
                    case AVPixelFormat.AV_PIX_FMT_RGBA: return new Pixel(r: p0[r4 + 0], g: p0[r4 + 1], b: p0[r4 + 2], a: p0[r4 + 3]);
                    case AVPixelFormat.AV_PIX_FMT_ABGR: return new Pixel(a: p0[r4 + 0], b: p0[r4 + 1], g: p0[r4 + 2], r: p0[r4 + 3]);
                    case AVPixelFormat.AV_PIX_FMT_BGRA: return new Pixel(b: p0[r4 + 0], g: p0[r4 + 1], r: p0[r4 + 2], a: p0[r4 + 3]);
                    case AVPixelFormat.AV_PIX_FMT_RGB24: {
                        int r3 = x * 3 + y * s[0];
                        return new Pixel(r: p0[r3 + 0], g: p0[r3 + 3], b: p0[r3 + 2], a: (byte)255);
                    }
                    case AVPixelFormat.AV_PIX_FMT_BGR24: {
                        int r3 = x * 3 + y * s[0];
                        return new Pixel(b: p0[r3 + 0], g: p0[r3 + 3], r: p0[r3 + 2], a: (byte)255);
                    }
                    case AVPixelFormat.AV_PIX_FMT_YUV420P: {
                        return Yuv2Rgb(
                            p[0][x + y * s[0]],
                            p[1][(x / 2) + (y / 2) * s[1]],
                            p[2][(x / 2) + (y / 2) * s[2]]
                        );
                    }
                    case AVPixelFormat.AV_PIX_FMT_YUV444P: {
                        return Yuv2Rgb(
                            p[0][x + y * s[0]],
                            p[1][x + y * s[1]],
                            p[2][x + y * s[2]]
                        );
                    }
                    default: throw new NotSupportedException();
                }
            }
            return default;
        }
        public void SetPixel(int x, int y, Pixel px)
        {
            var frame = Frame;
            if (x >= 0 && y >= 0 && x < frame->width && y < frame->height) {
                byte** p = frame->extended_data;
                int* s = (int*)&frame->linesize;
                byte* p0 = p[0];
                int r4 = x * 4 + y * s[0];
                switch (Format) {
                    case AVPixelFormat.AV_PIX_FMT_ARGB: p0[r4 + 0] = px.A; p0[r4 + 1] = px.R; p0[r4 + 2] = px.G; p0[r4 + 3] = px.B; break;
                    case AVPixelFormat.AV_PIX_FMT_RGBA: p0[r4 + 0] = px.R; p0[r4 + 1] = px.G; p0[r4 + 2] = px.B; p0[r4 + 3] = px.A; break;
                    case AVPixelFormat.AV_PIX_FMT_ABGR: p0[r4 + 0] = px.A; p0[r4 + 1] = px.B; p0[r4 + 2] = px.G; p0[r4 + 3] = px.R; break;
                    case AVPixelFormat.AV_PIX_FMT_BGRA: p0[r4 + 0] = px.B; p0[r4 + 1] = px.G; p0[r4 + 2] = px.R; p0[r4 + 3] = px.A; break;
                    case AVPixelFormat.AV_PIX_FMT_RGB24: {
                        int r3 = x * 3 + y * s[0];
                        p0[r3 + 0] = px.R; p0[r3 + 1] = px.G; p0[r3 + 2] = px.B;
                        break;
                    }
                    case AVPixelFormat.AV_PIX_FMT_BGR24: {
                        int r3 = x * 3 + y * s[0];
                        p0[r3 + 0] = px.B; p0[r3 + 1] = px.G; p0[r3 + 2] = px.R;
                        break;
                    }
                    case AVPixelFormat.AV_PIX_FMT_YUV420P: {
                        Rgb2Yuv(
                            px, 
                            out p[0][x + y * s[0]],
                            out p[1][(x / 2) + (y / 2) * s[1]],
                            out p[2][(x / 2) + (y / 2) * s[2]]
                        );
                        break;
                    }
                    case AVPixelFormat.AV_PIX_FMT_YUV444P: {
                        Rgb2Yuv(
                            px, 
                            out p[0][x + y * s[0]], 
                            out p[1][x + y * s[1]], 
                            out p[2][x + y * s[2]]
                        );
                        break;
                    }
                    default: throw new NotSupportedException();
                }
            }
        }

        private static Pixel Yuv2Rgb(byte y, byte u, byte v)
        {
            int cb = u - 128;
            int cr = v - 128;
            int r = (int)(y + 1.402 * cr);
            int g = (int)(y - 0.344 * cb - 0.714 * cr);
            int b = (int)(y + 1.772 * cb);
            return new Pixel(r, g, b, 255);
        }
        private static void Rgb2Yuv(Pixel p, out byte y, out byte u, out byte v)
        {
            y = (byte)( 0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
            u = (byte)(-0.169 * p.R - 0.331 * p.G + 0.499 * p.B + 128);
            v = (byte)( 0.499 * p.R - 0.418 * p.G - 0.081 * p.B + 128);
        }
    }
}
