using System;
using FFmpeg.AutoGen;

namespace FFmpegWrapper
{
    public unsafe class SwScaler : IDisposable
    {
        private SwsContext* _ctx;

        public SwsContext* Context
        {
            get {
                ThrowIfDisposed();
                return _ctx;
            }
        }

        private bool _disposed = false;

        public PictureInfo SourceFormat { get; }
        public PictureInfo DestinationFormat { get; }

        public SwScaler(PictureInfo srcFmt, PictureInfo dstFmt, InterpolationMode flags = InterpolationMode.Bicubic)
        {
            SourceFormat = srcFmt;
            DestinationFormat = dstFmt;

            _ctx = ffmpeg.sws_getContext(srcFmt.Width, srcFmt.Height, srcFmt.PixelFormat,
                                         dstFmt.Width, dstFmt.Height, dstFmt.PixelFormat,
                                         (int)flags, null, null, null);
        }
        public int Scale(Picture src, Picture dst)
        {
            CheckFormats(src.Format, src.Width, src.Height,
                         dst.Format, dst.Width, dst.Height);

            return Scale(src.Planes, src.Strides, dst.Planes, dst.Strides);
        }
        public int Scale(AVFrame* src, AVFrame* dst)
        {
            CheckFormats((AVPixelFormat)src->format, src->width, src->height,
                         (AVPixelFormat)dst->format, dst->width, dst->height);

            return Scale((byte**)&src->data, (int*)&src->linesize, (byte**)&dst->data, (int*)&dst->linesize);
        }
        public int Scale(byte*[] src, int[] srcStride, byte*[] dst, int[] dstStride)
        {
            return ffmpeg.sws_scale(Context, src, srcStride, 0, SourceFormat.Height, dst, dstStride).CheckError("Failed to rescale frame");
        }
        public int Scale(byte** src, int* srcStride, byte** dst, int* dstStride)
        {
            return ffmpegex.sws_scale(Context, src, srcStride, 0, SourceFormat.Height, dst, dstStride).CheckError("Failed to rescale frame");
        }

        private void CheckFormats(AVPixelFormat src, int srcW, int srcH, AVPixelFormat dst, int dstW, int dstH)
        {
            var srcFmt = SourceFormat;
            var dstFmt = DestinationFormat;

            if (src != srcFmt.PixelFormat || srcW != srcFmt.Width || srcH != srcFmt.Height) {
                throw new ArgumentException("Resolution and pixel format must be the same as SourceFormat", "src");
            }
            if (dst != dstFmt.PixelFormat || dstW != dstFmt.Width || dstH != dstFmt.Height) {
                throw new ArgumentException("Resolution and pixel format must be the same as DestinationFormat", "dst");
            }
        }

        public void Dispose()
        {
            if (!_disposed) {
                ffmpeg.sws_freeContext(Context);
                _disposed = true;
            }
        }
        private void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(SwScaler));
            }
        }
    }
    public enum InterpolationMode
    {
        FastBilinear = ffmpeg.SWS_FAST_BILINEAR,
        Bilinear = ffmpeg.SWS_BILINEAR,
        Bicubic = ffmpeg.SWS_BICUBIC,
        NearestNeighbor = ffmpeg.SWS_POINT,
        Box = ffmpeg.SWS_AREA,
        Gaussian = ffmpeg.SWS_GAUSS,
        Sinc = ffmpeg.SWS_SINC,
        Lanczos = ffmpeg.SWS_LANCZOS,
        Spline = ffmpeg.SWS_SPLINE
    }
}
