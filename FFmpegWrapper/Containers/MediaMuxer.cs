using System;
using System.Collections.Generic;
using System.Linq;
using FFmpeg.AutoGen;
using FFmpegWrapper.Codec;

namespace FFmpegWrapper.Container
{
    public unsafe class MediaMuxer : IDisposable
    {
        private AVFormatContext* _ctx; //be careful when using directly.
        private bool _disposed = false;
        private bool _leaveIoCtxOpen = true;
        private List<MediaStream> _streams = new List<MediaStream>();

        public AVFormatContext* Context
        {
            get {
                ThrowIfDisposed();
                return _ctx;
            }
        }
        public string Filename { get; }
        public IOContext IOContext { get; }

        public IReadOnlyList<MediaStream> Streams => _streams;

        public bool IsOpen { get; private set; } = false;

        public MediaMuxer(IOContext ioContext, ContainerType format, bool leaveOpen = true)
            : this(ioContext, format.GetOutputFormat(), leaveOpen)
        {
        }
        public MediaMuxer(IOContext ioContext, AVOutputFormat* format, bool leaveOpen = true)
        {
            _leaveIoCtxOpen = leaveOpen;
            IOContext = ioContext;

            try {
                _ctx = ffmpeg.avformat_alloc_context();
                if (_ctx == null) {
                    throw new OutOfMemoryException("Could not allocate muxer");
                }
                _ctx->oformat = format;
                _ctx->pb = ioContext.Context;
            } catch {
                ffmpeg.avformat_free_context(_ctx);
                throw;
            }
        }
        public MediaMuxer(string filename)
        {
            fixed (AVFormatContext** fmtCtx = &_ctx) {
                ffmpeg.avformat_alloc_output_context2(fmtCtx, null, null, filename).CheckError("Could not allocate muxer");
            }
            Filename = filename;
        }

        public MediaStream AddStream(MediaEncoder encoder)
        {
            ThrowIfDisposed();
            if (IsOpen) {
                throw new InvalidOperationException("Stream can only be added before opening the muxer.");
            }

            AVCodecContext* cc = encoder.Context;

            AVStream* stream = ffmpeg.avformat_new_stream(_ctx, cc->codec);
            if (stream == null) {
                throw new OutOfMemoryException("Could not allocate stream");
            }
            stream->id = (int)_ctx->nb_streams - 1;
            stream->time_base = encoder.TimeBase;
            stream->codec->time_base = encoder.TimeBase;
            stream->codec->framerate = encoder.FrameRate;

            //Some formats want stream headers to be separate.
            if ((_ctx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0) {
                cc->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
            }

            var st = new MediaStream(stream, MediaStreamMode.Encode, encoder);
            _streams.Add(st);
            return st;
        }

        /// <summary> Opens all streams and write the container header. </summary>
        public void Open()
        {
            ThrowIfDisposed();
            if (IsOpen) {
                return;
            }

            if (_streams.Any(s => !s.Codec.IsOpen)) {
                throw new InvalidOperationException($"All streams must be open before opening the muxer.");
            }

            foreach (var stream in _streams) {
                ffmpeg.avcodec_parameters_from_context(stream.Stream->codecpar, stream.Codec.Context).CheckError("Could not copy the encoder parameters to the stream.");
            }

            if (IOContext == null) {
                ffmpeg.avio_open(&_ctx->pb, Filename, ffmpeg.AVIO_FLAG_WRITE).CheckError("Could not open output file");
            }

            ffmpeg.avformat_write_header(_ctx, null).CheckError("Could not write header to output file");

            IsOpen = true;
        }

        public void Write(MediaStream stream, MediaPacket packet)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            var pkt = new AVPacket();

            var mem = packet.Data;
            fixed (byte* pData = mem.Span) {
                pkt.stream_index = stream.Index;
                pkt.data = pData;
                pkt.size = mem.Length;

                pkt.pts = packet.PresentationTimestamp ?? ffmpeg.AV_NOPTS_VALUE;
                pkt.dts = packet.DecompressionTimestamp ?? ffmpeg.AV_NOPTS_VALUE;
                pkt.duration = packet.Duration;

                ffmpeg.av_packet_rescale_ts(&pkt, stream.Codec.TimeBase, stream.Stream->time_base);
                ffmpeg.av_interleaved_write_frame(_ctx, &pkt).CheckError("Failed to write frame");
            }
        }
        public void Write(AVPacket* packet)
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();

            ffmpeg.av_interleaved_write_frame(_ctx, packet).CheckError("Failed to write frame");
        }

        private void ThrowIfNotOpen()
        {
            if (!IsOpen) {
                throw new InvalidOperationException("Muxer is not open");
            }
        }
        private void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(MediaMuxer));
            }
        }

        /// <summary> Writes the container trailer and disposes the muxer. </summary>
        public void Dispose()
        {
            if (!_disposed) {
                ffmpeg.av_write_trailer(_ctx);

                if (IOContext != null && !_leaveIoCtxOpen) {
                    IOContext.Dispose();
                }

                ffmpeg.avformat_free_context(_ctx);
                _disposed = true;
            }
        }
    }
}
