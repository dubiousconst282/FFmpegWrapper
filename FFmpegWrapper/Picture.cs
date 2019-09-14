using System;
using System.IO;
using FFmpeg.AutoGen;
using FFmpegWrapper.Codec;

namespace FFmpegWrapper
{
    public unsafe partial class Picture : IDisposable
    {
        private AVFrame* _frame;
        public AVFrame* Frame
        {
            get {
                ThrowIfDisposed();
                return _frame;
            }
        }

        public int Width { get; }
        public int Height { get; }
        public AVPixelFormat Format { get; }
        public PictureInfo Info => new PictureInfo(Width, Height, Format);

        /// <summary> Pointer to the packed pixel data, or the first plane if the pixel format is planar. </summary>
        public byte* Data => Frame->extended_data[0];

        /// <summary> Pointer to the pixel planes. (4 elements) </summary>
        public byte** Planes => Frame->extended_data;

        /// <summary> Line size for each plane. (4 elements) </summary>
        public int* Strides => (int*)&Frame->linesize;

        private bool _ownFrame = true;
        private bool _disposed = false;

        public Picture(PictureInfo fmt)
            : this(fmt.Width, fmt.Height, fmt.PixelFormat)
        {
        }
        public Picture(int w, int h, AVPixelFormat fmt = AVPixelFormat.AV_PIX_FMT_RGBA, bool clearToBlack = false)
        {
            if (w <= 0 || h <= 0) {
                throw new ArgumentException("Invalid frame resolution.");
            }
            _frame = ffmpeg.av_frame_alloc();
            _frame->format = (int)fmt;
            _frame->width = w;
            _frame->height = h;

            ffmpeg.av_frame_get_buffer(_frame, 0).CheckError("Failed to allocate frame.");

            Width = w;
            Height = h;
            Format = fmt;

            if (clearToBlack) {
                Clear();
            }
        }
        public Picture(AVFrame* frame, bool clearToBlack = false, bool freeOnDispose = false)
        {
            if (frame->width <= 0 || frame->height <= 0 || frame->extended_data == null) {
                throw new ArgumentException("Invalid frame.");
            }
            _frame = frame;
            _ownFrame = freeOnDispose;

            Width = frame->width;
            Height = frame->height;
            Format = (AVPixelFormat)frame->format;

            if (clearToBlack) {
                Clear();
            }
        }

        /// <summary> Fills this picture with black pixels. </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            var frame = _frame;

            var data = new byte_ptrArray4();
            var strides = new long_array4();

            for (uint i = 0; i < 4; i++) {
                data[i] = frame->data[i];
                strides[i] = frame->linesize[i];
            }

            ffmpeg.av_image_fill_black(ref data, strides, Format, frame->color_range, frame->width, frame->height).CheckError("Failed to clear frame.");
        }

        public void CopyTo(AVFrame* frame)
        {
            ThrowIfDisposed();

            ffmpegex.av_image_copy((byte**)&frame->data, (int*)&frame->linesize,
                                   (byte**)&_frame->data, (int*)&_frame->linesize, Format,
                                   Math.Min(Width, frame->width), Math.Min(Height, frame->height));
        }
        public void CopyFrom(AVFrame* frame)
        {
            ThrowIfDisposed();

            ffmpegex.av_image_copy((byte**)&_frame->data, (int*)&_frame->linesize,
                                   (byte**)&frame->data, (int*)&frame->linesize, Format,
                                   Math.Min(Width, frame->width), Math.Min(Height, frame->height));
        }

        public void Save(string path, int quality = 90, int dstW = 0, int dstH = 0)
        {
            ThrowIfDisposed();

            bool jpeg = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

            var codec = jpeg ? AVCodecID.AV_CODEC_ID_MJPEG : AVCodecID.AV_CODEC_ID_PNG;
            var pixFmt = jpeg ? AVPixelFormat.AV_PIX_FMT_YUVJ444P : AVPixelFormat.AV_PIX_FMT_RGBA;

            if (dstW <= 0) dstW = Width;
            if (dstH <= 0) dstH = Height;

            using (var tmp = new Picture(dstW, dstH, pixFmt))
            using (var enc = new VideoEncoder(codec, dstW, dstH, tmp.Format, 1, 10000))
            using (var sws = new SwScaler(Info, tmp.Info)) {
                var ctx = enc.Context;
                if (jpeg) {
                    //1-31
                    int q = 1 + (100 - quality) * 31 / 100;
                    enc.MaxQuantizer = q;
                    enc.MinQuantizer = q;
                } else {
                    //zlib compression (0-9)
                    enc.CompressionLevel = quality * 9 / 100;
                }
                enc.Open();

                sws.Scale(this, tmp);
                enc.SendFrame(tmp, 0);

                var packet = new MediaPacket();
                enc.ReceivePacket(packet);

                File.WriteAllBytes(path, packet.Data.ToArray());
            }
        }

        public void Dispose()
        {
            if (!_disposed) {
                if (_ownFrame) { fixed (AVFrame** ppFrame = &_frame) ffmpeg.av_frame_free(ppFrame); }
                _disposed = true;
            }
        }
        private void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(Picture));
            }
        }
    }
}
