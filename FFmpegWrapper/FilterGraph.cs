using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;

namespace FFmpegWrapper
{
    //TODO: impl
    internal unsafe class FilterGraph : IDisposable
    {
        public static IReadOnlyList<string> AllFilters { get; } = GetFilterNames();

        private bool _disposed = false;
        private AVFilterGraph* _graph;
        public AVFilterContext* _src;
        public AVFilterContext* _sink;

        public AVFilterContext* BufferSource
        {
            get {
                ThrowIfDisposed();
                return _src;
            }
        }
        public AVFilterContext* BufferSink
        {
            get {
                ThrowIfDisposed();
                return _sink;
            }
        }
        public AVFilterGraph* Graph
        {
            get {
                ThrowIfDisposed();
                return _graph;
            }
        }

        public FilterGraph(PictureInfo fmt, double fps)
            : this(fmt, new AVRational() { den = (int)Math.Round(fps * 1000), num = 1000 })
        {
        }
        public FilterGraph(PictureInfo fmt, AVRational timeBase)
        {
            _graph = ffmpeg.avfilter_graph_alloc();

            var buffersrc = ffmpeg.avfilter_get_by_name("buffersrc");
            var buffersink = ffmpeg.avfilter_get_by_name("buffersink");

            fixed (AVFilterContext** src = &_src, sink = &_sink) {
                string args = $"video_size={fmt.Width}x{fmt.Height}:pix_fmt={(int)fmt.PixelFormat}:time_base={timeBase.num}/{timeBase.den}";

                ffmpeg.avfilter_graph_create_filter(src, buffersrc, "in", args, null, _graph).CheckError("Could not create buffer source");

                ffmpeg.avfilter_graph_create_filter(sink, buffersink, "out", null, null, _graph).CheckError("Could not create buffer sink");
            }
        }

        public void SetSinkOption(string key, long value, int searchFlags = ffmpeg.AV_OPT_SEARCH_CHILDREN)
        {
            ffmpeg.av_opt_set_int(_sink, key, value, searchFlags).CheckError("Failed to set option value.");
        }
        public void SetSourceOption(string key, long value, int searchFlags = ffmpeg.AV_OPT_SEARCH_CHILDREN)
        {
            ffmpeg.av_opt_set_int(_src, key, value, searchFlags).CheckError("Failed to set option value.");
        }

        private static List<string> GetFilterNames()
        {
            var list = new List<string>(512);

            AVFilter* filter;
            void* ptr = null;

            while ((filter = ffmpeg.av_filter_iterate(&ptr)) != null) {
                list.Add(new string((sbyte*)filter->name));
            }
            return list;
        }

        public void Dispose()
        {
            if (!_disposed) {
                fixed (AVFilterGraph** g = &_graph) ffmpeg.avfilter_graph_free(g);
                _disposed = true;
            }
        }
        private void ThrowIfDisposed()
        {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(FilterGraph));
            }
        }
    }
}
