using System;
using System.IO;
using FFmpeg.AutoGen;
using FFmpegWrapper.Codec;

namespace FFmpegWrapper
{
    public unsafe partial class Picture : MediaFrame
    {
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

        public Picture(PictureInfo fmt)
            : this(fmt.Width, fmt.Height, fmt.PixelFormat)
        {
        }
        public Picture(int width, int height, AVPixelFormat fmt = AVPixelFormat.AV_PIX_FMT_RGBA, bool clearToBlack = false)
        {
            if (width <= 0 || height <= 0) {
                throw new ArgumentException("Invalid frame resolution.");
            }
            _frame = ffmpeg.av_frame_alloc();
            _frame->format = (int)fmt;
            _frame->width = width;
            _frame->height = height;

            ffmpeg.av_frame_get_buffer(_frame, 0).CheckError("Failed to allocate frame.");

            Width = width;
            Height = height;
            Format = fmt;

            if (clearToBlack) {
                Clear();
            }
        }
        /// <summary>
        /// Creates an new Picture instance with an already allocated frame.
        /// </summary>
        /// <param name="freeOnDispose">If true, the frame will be freed with av_frame_free() when you call Dispose().</param>
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

        /// <summary> Saves this picture to the specified file. The format will be choosen based on the file extension. (Can be either JPG or PNG) </summary>
        /// <param name="quality">JPEG: Quantization factor. PNG: ZLib compression level. 0-100</param>
        public void Save(string filename, int quality = 90, int dstW = 0, int dstH = 0)
        {
            ThrowIfDisposed();

            bool jpeg = filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

            var codec = jpeg ? AVCodecID.AV_CODEC_ID_MJPEG : AVCodecID.AV_CODEC_ID_PNG;
            var pixFmt = jpeg ? AVPixelFormat.AV_PIX_FMT_YUVJ444P : AVPixelFormat.AV_PIX_FMT_RGBA;

            if (dstW <= 0) dstW = Width;
            if (dstH <= 0) dstH = Height;

            using var tmp = new Picture(dstW, dstH, pixFmt);
            using var enc = new VideoEncoder(codec, dstW, dstH, tmp.Format, 1, 10000);
            using var sws = new SwScaler(Info, tmp.Info);

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

            File.WriteAllBytes(filename, packet.Data.ToArray());
        }
    }
}
