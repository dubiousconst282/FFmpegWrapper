namespace FFmpeg.Wrapper;

partial class MediaFilterPipeline
{
    public unsafe class Builder
    {
        private MediaFilterPipeline _pipe = new();

        /// <summary> The underlying filter graph. </summary>
        public MediaFilterGraph Graph => _pipe._graph;

        /// <summary> Map of currently available input ports. </summary>
        public Dictionary<string, MediaFilterNodePort> ActivePorts { get; set; } = new();

        public Builder VideoBufferSource(PictureFormat inputFormat, PictureColorspace colorspace, Rational timeBase, Rational? frameRate, string name = "in")
        {
            if (_pipe._sources.ContainsKey(name)) {
                throw new InvalidOperationException($"A buffer source with the same port name '{name}' already exists.");
            }
            var source = Graph.AddVideoBufferSource(inputFormat, colorspace, timeBase, frameRate);
            _pipe._sources[name] = source;
            ActivePorts[name] = source.GetOutput(0);
            return this;
        }

        /// <summary> Creates a video buffer source using parameters from the given decoder. </summary>
        /// <param name="autoRotate"> Whether to auto rotate the input based on the display matrix present in metadata (if any). </param>
        public Builder VideoBufferSource(VideoDecoder decoder, bool autoRotate, string name = "in")
        {
            VideoBufferSource(decoder.FrameFormat, decoder.Colorspace, decoder.TimeBase, decoder.FrameRate, name);

            if (autoRotate) {
                string? desc = GetAutoRotateFilterString(decoder.CodedSideData.GetDisplayMatrix());

                if (desc != null) {
                    PassthroughSegment(desc, name, MediaTypes.Video);
                }
            }
            return this;
        }
        
        /// <inheritdoc cref="VideoBufferSource(VideoDecoder, bool, string)"/>
        /// <param name="rotatedSize"> Frame size that is to be adjusted based on <paramref name="autoRotate"/>. </param>
        public Builder VideoBufferSource(VideoDecoder decoder, ref PictureFormat rotatedSize, bool autoRotate, string name = "in")
        {
            VideoBufferSource(decoder.FrameFormat, decoder.Colorspace, decoder.TimeBase, decoder.FrameRate, name);
            rotatedSize = decoder.FrameFormat;

            if (autoRotate) {
                string? desc = GetAutoRotateFilterString(decoder.CodedSideData.GetDisplayMatrix());

                if (desc != null) {
                    PassthroughSegment(desc, name, MediaTypes.Video);
                }
                if (desc != null && desc.Contains("transpose")) {
                    rotatedSize = new PictureFormat(
                        rotatedSize.Height, rotatedSize.Width, 
                        rotatedSize.PixelFormat, rotatedSize.PixelAspectRatio.Reciprocal()
                    );
                }
            }
            return this;
        }

        /// <param name="timeBase"> Null defaults to <c>1/sampleRate</c>. </param>
        public Builder AudioBufferSource(AudioFormat format, Rational? timeBase = null, string name = "a_in")
        {
            if (_pipe._sources.ContainsKey(name)) {
                throw new InvalidOperationException($"A buffer source with the same port name '{name}' already exists.");
            }
            var source = Graph.AddAudioBufferSource(format, timeBase ?? new Rational(1, format.SampleRate));
            _pipe._sources[name] = source;
            ActivePorts[name] = source.GetOutput(0);
            return this;
        }

        /// <summary> Creates an audio buffer source using parameters from the given decoder. </summary>
        public Builder AudioBufferSource(AudioDecoder decoder, string name = "a_in")
        {
            return AudioBufferSource(decoder.Format, decoder.TimeBase, name);
        }

        /// <summary> Appends a filter segment described by the given string. </summary>
        /// <remarks> https://ffmpeg.org/ffmpeg-filters.html#Filtergraph-syntax-1 </remarks>
        public Builder Segment(string graphString)
        {
            var segmentOutputs = Graph.Parse(graphString, ActivePorts.Select(e => (e.Key, e.Value)).ToArray());

            // Note: Parse() will forward unused inputs to output map.
            ActivePorts = segmentOutputs;

            RenamePort("out", "in");
            RenamePort("a_out", "a_in");

            return this;
        }

        /// <summary> Appends a resizing filter based on libswscale. </summary>
        /// <remarks> https://ffmpeg.org/ffmpeg-filters.html#scale-1 </remarks>
        /// <param name="destColorspace"> If non-null, specifies output color space. Note that swscale only supports color range and matrix conversions. </param>
        public unsafe Builder SwScale(PictureFormat destFormat, PictureColorspace? destColorspace = null, InterpolationMode flags = InterpolationMode.Bicubic | InterpolationMode.HighQuality, string? portName = null)
        {
            return PassthroughSegment(GetSwScaleFilterString(destFormat, destColorspace, flags), portName, MediaTypes.Video);
        }

        /// <remarks> https://ffmpeg.org/ffmpeg-filters.html#crop </remarks>
        public Builder Crop(int x, int y, int width, int height, string? portName = null)
        {
            return PassthroughSegment($"crop=x={x}:y={y}:w={width}:h={height}", portName, MediaTypes.Video);
        }

        private Builder PassthroughSegment(string graphStr, string? portName, AVMediaType type)
        {
            portName ??= ActivePorts.Single(p => p.Value.Type == type).Key;
            string tempName = portName + "__tmp";

            Segment($"[{portName}] {graphStr} [{tempName}]");
            RenamePort(tempName, portName);

            return this;
        }

        /// <summary> Creates indentical copies of the specified port, sequentially named <c>inputPort_0</c>...<c>inputPort_N</c>. </summary> 
        public Builder Split(string inputPort, int count)
        {
            return Split(inputPort, Enumerable.Range(0, count).Select(i => inputPort + "_" + i).ToArray());
        }

        public Builder Split(string[] outputPorts)
        {
            return Split(ActivePorts.Single().Key, outputPorts);
        }

        /// <summary> Creates indentical copies of the specified port. </summary>
        /// <remarks> https://ffmpeg.org/ffmpeg-filters.html#split_002c-asplit </remarks>
        public Builder Split(string inputPort, string[] outputPorts)
        {
            if (outputPorts.Length < 2) {
                throw new ArgumentException("Split filter must have at least two output ports.");
            }
            bool isAudio = ActivePorts[inputPort].Type == MediaTypes.Audio;
            Segment($"[{inputPort}] {(isAudio ? "asplit" : "split")}={outputPorts.Length} [{string.Join("][", outputPorts)}]");
            return this;
        }

        private void RenamePort(string oldName, string newName)
        {
            if (ActivePorts.TryGetValue(oldName, out var port)) {
                ActivePorts.Remove(oldName);
                ActivePorts.Add(newName, port);
            }
        }

        public MediaFilterPipeline Build()
        {
            foreach (var (name, port) in ActivePorts) {
                if (port.IsConnected) continue;

                string outName = name == "in" ? "out" : name == "a_in" ? "a_out" : name;

                _pipe._sinks.Add(outName, port.Type switch {
                    MediaTypes.Video => Graph.AddVideoBufferSink(port),
                    MediaTypes.Audio => Graph.AddAudioBufferSink(port),
                });
            }
            Graph.Configure();

            var pipe = _pipe;
            _pipe = null!; // minor hack to prevent users from reusing this builder
            return pipe;
        }
    }

    public static string? GetAutoRotateFilterString(int[]? displayMatrix)
    {
        if (displayMatrix == null) return null;

        // https://github.com/FFmpeg/FFmpeg/blob/7b47099bc080ee597327476c0df44d527c349862/fftools/ffmpeg_filter.c#L1711
        double angle = ffmpeg.av_display_rotation_get(in Unsafe.As<int, int_array9>(ref displayMatrix[0])); // counterclockwise. in range [-180.0, 180.0]
        if (double.IsNaN(angle)) return null;

        // https://github.com/FFmpeg/FFmpeg/blob/cdcb4b98b7f74d87a6274899ff70724795d551cb/fftools/cmdutils.c#L1107 
        angle = -Math.Round(angle); // clockwise
        angle -= 360 * Math.Floor((angle / 360) + (0.9 / 360)); // clamp to [0, 360)

        int theta = (int)angle;
        string desc = string.Empty;

        if (theta == 90) {
            desc = displayMatrix[3] > 0 ? "transpose=cclock_flip" : "transpose=clock";
        } else if (theta == 180) {
            if (displayMatrix[0] < 0) {
                desc += "hflip";
            }
            if (displayMatrix[4] < 0) {
                desc += desc.Length > 0 ? ",vflip" : "vflip";
            }
        } else if (theta == 270) {
            desc = displayMatrix[3] < 0 ? "transpose=clock_flip" : "transpose=cclock";
        } else if (theta == 0) {
            if (displayMatrix[4] < 0) {
                desc = "vflip";
            }
        }
        return desc.Length == 0 ? null : desc;
    }

    public static unsafe string GetSwScaleFilterString(PictureFormat destFormat, PictureColorspace? destColorspace = null, InterpolationMode flags = InterpolationMode.Bicubic | InterpolationMode.HighQuality)
    {
        string desc = $"scale=width={destFormat.Width}:height={destFormat.Height}:flags=";

        // Convert flags to string because vf_scale only accepts a string.
        for (var opt = ffmpeg.sws_get_class()->option; opt->name != null; opt++) {
            if (opt->type != AVOptionType.AV_OPT_TYPE_CONST) continue;
            if (!Helpers.StrCmp(opt->unit, "sws_flags"u8)) continue;
            if (((long)flags & opt->default_val.i64) == 0) continue;

            if (!desc.EndsWith("=")) desc += '+';
            desc += Helpers.PtrToStringUTF8(opt->name);
        }

        if (destColorspace != null) {
            desc += $"out_color_matrix={(int)destColorspace.Value.Matrix}:out_range={(int)destColorspace.Value.Range}";
        }
        return desc;
    }
}