namespace FFmpeg.Wrapper;

public unsafe class VideoFrame : MediaFrame
{
    public int Width => _frame->width;
    public int Height => _frame->height;
    public AVPixelFormat PixelFormat => (AVPixelFormat)_frame->format;

    public PictureFormat Format => new(Width, Height, PixelFormat, _frame->sample_aspect_ratio);
    public PictureColorspace Colorspace {
        get => new(_frame->colorspace, _frame->color_primaries, _frame->color_trc, _frame->color_range);
        set {
            ThrowIfDisposed();
            _frame->colorspace = value.Matrix;
            _frame->color_primaries = value.Primaries;
            _frame->color_trc = value.Transfer;
            _frame->color_range = value.Range;
        }
    }

    /// <summary> Pointers to the pixel data planes. </summary>
    /// <remarks> These can point to the end of image data when used in combination with negative values in <see cref="RowSize"/>. </remarks>
    public byte** Data => (byte**)&_frame->data;

    /// <summary> An array of positive or negative values indicating the size in bytes of each pixel row. </summary>
    /// <remarks> 
    /// - Values may be larger than the size of usable data -- there may be extra padding present for performance reasons. <br/>
    /// - Values can be negative to achieve a vertically inverted iteration over image rows.
    /// </remarks>
    public int* RowSize => (int*)&_frame->linesize;

    /// <summary> Whether this frame is attached to a hardware frame context. </summary>
    public bool IsHardwareFrame => _frame->hw_frames_ctx != null;

    /// <summary> Whether the frame rows are flipped. Alias for <c>RowSize[0] &lt; 0</c>. </summary>
    public bool IsVerticallyFlipped => _frame->linesize[0] < 0;

    /// <summary> Allocates an empty <see cref="AVFrame"/>. </summary>
    public VideoFrame()
        : this(ffmpeg.av_frame_alloc(), takeOwnership: true) { }

    public VideoFrame(PictureFormat fmt, bool clearToBlack = true)
        : this(fmt.Width, fmt.Height, fmt.PixelFormat, clearToBlack) { }

    public VideoFrame(int width, int height, AVPixelFormat fmt, bool clearToBlack = true)
    {
        if (width <= 0 || height <= 0) {
            throw new ArgumentException("Invalid frame dimensions.");
        }
        _frame = ffmpeg.av_frame_alloc();
        _frame->format = (int)fmt;
        _frame->width = width;
        _frame->height = height;

        ffmpeg.av_frame_get_buffer(_frame, 0).CheckError("Failed to allocate frame buffers.");

        if (clearToBlack) {
            Clear();
        }
    }
    /// <summary> Wraps an existing <see cref="AVFrame"/> pointer. </summary>
    /// <param name="takeOwnership">True if <paramref name="frame"/> should be freed when Dispose() is called.</param>
    public VideoFrame(AVFrame* frame, bool takeOwnership, bool clearToBlack = false)
    {
        if (frame == null) {
            throw new ArgumentNullException(nameof(frame));
        }
        _frame = frame;
        _ownsFrame = takeOwnership;

        if (clearToBlack) {
            Clear();
        }
    }

    /// <summary> Returns a view over the pixel row for the specified plane. </summary>
    /// <remarks> The returned span may be longer than <see cref="Width"/> due to padding. </remarks>
    /// <param name="y">Row index, in top to bottom order.</param>
    public Span<T> GetRowSpan<T>(int y, int plane = 0) where T : unmanaged
    {
        if ((uint)y >= (uint)GetPlaneSize(plane).Height) {
            throw new ArgumentOutOfRangeException();
        }
        int stride = RowSize[plane];
        return new Span<T>(&Data[plane][y * stride], Math.Abs(stride / sizeof(T)));
    }

    /// <summary> Returns a view over the pixel data for the specified plane. </summary>
    /// <remarks> Note that rows may be stored in reverse order depending on <see cref="IsVerticallyFlipped"/>. </remarks>
    /// <param name="stride">Number of pixels per row.</param>
    public Span<T> GetPlaneSpan<T>(int plane, out int stride) where T : unmanaged
    {
        int height = GetPlaneSize(plane).Height;

        byte* data = _frame->data[(uint)plane];
        int rowSize = _frame->linesize[(uint)plane];

        if (rowSize < 0) {
            data += rowSize * (height - 1);
            rowSize *= -1;
        }
        stride = rowSize / sizeof(T);
        return new Span<T>(data, checked(height * stride));
    }

    public (int Width, int Height) GetPlaneSize(int plane)
    {
        ThrowIfDisposed();

        var size = (Width, Height);

        //https://github.com/FFmpeg/FFmpeg/blob/c558fcf41e2027a1096d00b286954da2cc4ae73f/libavutil/imgutils.c#L111
        if (plane == 0) {
            return size;
        }
        var desc = ffmpeg.av_pix_fmt_desc_get(PixelFormat);

        if (desc == null || (desc->flags & ffmpeg.AV_PIX_FMT_FLAG_HWACCEL) != 0) {
            throw new InvalidOperationException();
        }
        for (uint i = 0; i < 4; i++) {
            if (desc->comp[i].plane != plane) continue;

            if ((i == 1 || i == 2) && (desc->flags & ffmpeg.AV_PIX_FMT_FLAG_RGB) == 0) {
                size.Width = CeilShr(size.Width, desc->log2_chroma_w);
                size.Height = CeilShr(size.Height, desc->log2_chroma_h);
            }
            return size;
        }
        throw new ArgumentOutOfRangeException(nameof(plane));

        static int CeilShr(int x, int s) => (x + (1 << s) - 1) >> s;
    }

    /// <summary> Attempts to create a hardware frame memory mapping. Returns null if the backing device does not support frame mappings. </summary>
    public VideoFrame? Map(HardwareFrameMappingFlags flags)
    {
        ThrowIfDisposed();
        if (!IsHardwareFrame) {
            throw new InvalidOperationException("Cannot create mapping of non-hardware frame.");
        }

        var mapping = ffmpeg.av_frame_alloc();
        int result = ffmpeg.av_hwframe_map(mapping, _frame, (int)flags);

        if (result == 0) {
            mapping->width = _frame->width;
            mapping->height = _frame->height;
            return new VideoFrame(mapping, takeOwnership: true);
        }
        ffmpeg.av_frame_free(&mapping);
        return null;
    }
    /// <summary> Copy data from this frame to <paramref name="dest"/>. At least one of <see langword="this"/> or <paramref name="dest"/> must be a hardware frame. </summary>
    public void TransferTo(VideoFrame dest)
    {
        ThrowIfDisposed();
        ffmpeg.av_hwframe_transfer_data(dest.Handle, _frame, 0).CheckError("Failed to transfer data from hardware frame");
    }

    /// <summary> Gets an array of possible source or dest formats usable in <see cref="TransferTo(VideoFrame)"/>. </summary>
    public AVPixelFormat[] GetHardwareTransferFormats(HardwareFrameTransferDirection direction)
    {
        ThrowIfDisposed();
        if (!IsHardwareFrame) {
            throw new InvalidOperationException("Cannot query transfer formats for non-hardware frame.");
        }

        AVPixelFormat* pFormats;

        if (ffmpeg.av_hwframe_transfer_get_formats(_frame->hw_frames_ctx, (AVHWFrameTransferDirection)direction, &pFormats, 0) < 0) {
            return Array.Empty<AVPixelFormat>();
        }
        var formats = Helpers.GetSpanFromSentinelTerminatedPtr(pFormats, PixelFormats.None).ToArray();
        ffmpeg.av_freep(&pFormats);

        return formats;
    }

    /// <summary> Fills this frame with black pixels. </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        var linesizes = new long_array4();

        for (uint i = 0; i < 4; i++) {
            linesizes[i] = _frame->linesize[i];
        }
        ffmpeg.av_image_fill_black(
            ref *(byte_ptrArray4*)&_frame->data, linesizes,
            PixelFormat, _frame->color_range, _frame->width, _frame->height
        ).CheckError("Failed to clear frame.");
    }

    /// <summary> Saves this frame to the specified file. The format will be choosen based on the file extension. (Can be either JPG or PNG) </summary>
    /// <param name="quality">JPEG: Quantization factor. PNG: ZLib compression level. 0-100</param>
    public void Save(string filename, int quality = 90, int outWidth = 0, int outHeight = 0)
    {
        ThrowIfDisposed();

        if (IsHardwareFrame) {
            using var tmp = new VideoFrame();
            TransferTo(tmp);
            tmp.Save(filename, quality, outWidth, outHeight);
            return;
        }

        bool jpeg = filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

        var codec = jpeg ? AVCodecID.AV_CODEC_ID_MJPEG : AVCodecID.AV_CODEC_ID_PNG;
        var pixFmt = jpeg ? AVPixelFormat.AV_PIX_FMT_YUV444P : AVPixelFormat.AV_PIX_FMT_RGBA;

        if (outWidth <= 0) outWidth = Width;
        if (outHeight <= 0) outHeight = Height;

        // SwScale fails to convert color range when pixel format and resolution are equal. See #6
        if (jpeg && pixFmt == PixelFormat && outWidth == Width && outHeight == Height) {
            using var rgbFrame = new VideoFrame(outWidth, outHeight, PixelFormats.RGBA);

            // This seems to be redundant, but keeping for good sake.
            rgbFrame.Colorspace = new PictureColorspace(AVColorSpace.AVCOL_SPC_RGB, AVColorPrimaries.AVCOL_PRI_BT470M, AVColorTransferCharacteristic.AVCOL_TRC_GAMMA22, AVColorRange.AVCOL_RANGE_JPEG);

            using (var tempScaler = new SwScaler(Format, rgbFrame.Format, InterpolationMode.Bilinear | InterpolationMode.HighQuality)) {
                tempScaler.SetColorspace(Colorspace, rgbFrame.Colorspace);
                tempScaler.Convert(this, rgbFrame);
            }
            rgbFrame.Save(filename, quality, outWidth, outHeight);
            return;
        }

        using var tempFrame = new VideoFrame(outWidth, outHeight, pixFmt);
        using var encoder = new VideoEncoder(codec, tempFrame.Format, Rational.One);

        tempFrame.Colorspace = Colorspace;

        if (jpeg) {
            //1-31
            int q = 1 + (100 - quality) * 31 / 100;
            encoder.MaxQuantizer = q;
            encoder.MinQuantizer = q;
            encoder.Handle->color_range = AVColorRange.AVCOL_RANGE_JPEG;
            tempFrame.Handle->color_range = AVColorRange.AVCOL_RANGE_JPEG;
        } else {
            //zlib compression (0-9)
            encoder.CompressionLevel = quality * 9 / 100;
        }
        encoder.Open();

        var scalerMode = quality >= 80 ? InterpolationMode.Bicubic | InterpolationMode.HighQuality : InterpolationMode.Bilinear;
        using var sws = new SwScaler(Format, tempFrame.Format, scalerMode);
        sws.SetColorspace(this.Colorspace, tempFrame.Colorspace);
        sws.Convert(this, tempFrame);

        encoder.SendFrame(tempFrame);

        using var packet = new MediaPacket();
        encoder.ReceivePacket(packet);

        File.WriteAllBytes(filename, packet.Data.ToArray());
    }

    /// <summary> Decodes a single frame from the specified image or video file. </summary>
    /// <remarks> This method may be susceptible to DoS attacks. Do not use with untrusted inputs. </remarks>
    public static VideoFrame Load(string filename, TimeSpan? position = null)
    {
        using var demuxer = new MediaDemuxer(filename);
        var stream = demuxer.FindBestStream(MediaTypes.Video) ?? throw new FormatException();

        using var decoder = (VideoDecoder)demuxer.CreateStreamDecoder(stream);
        using var packet = new MediaPacket();

        var frame = new VideoFrame();

        if (position != null && !demuxer.Seek(position.Value, SeekOptions.Forward)) {
            //Position is past the stream duration, go back to the start or we won't get anything.
            demuxer.Seek(TimeSpan.Zero, SeekOptions.Backward);
        }

        while (demuxer.Read(packet)) {
            if (packet.StreamIndex != stream.Index) continue;

            decoder.SendPacket(packet);

            if (decoder.ReceiveFrame(frame)) {
                return frame;
            }
        }

        frame.Dispose();
        throw new FormatException();
    }
}
/// <summary> Flags to apply to hardware frame memory mappings. </summary>
public enum HardwareFrameMappingFlags
{
    /// <summary> The mapping must be readable. </summary>
    Read = 1 << 0,
    /// <summary> The mapping must be writeable. </summary>
    Write = 1 << 1,
    /// <summary>
    /// The mapped frame will be overwritten completely in subsequent
    /// operations, so the current frame data need not be loaded.  Any values
    /// which are not overwritten are unspecified.
    /// </summary>
    Overwrite = 1 << 2,
    /// <summary>
    /// The mapping must be direct.  That is, there must not be any copying in
    /// the map or unmap steps.  Note that performance of direct mappings may
    /// be much lower than normal memory.
    /// </summary>
    Direct = 1 << 3,
}
public enum HardwareFrameTransferDirection
{
    /// <summary> Transfer the data from the queried hw frame. </summary>
    From = AVHWFrameTransferDirection.AV_HWFRAME_TRANSFER_DIRECTION_FROM,
    /// <summary> Transfer the data to the queried hw frame. </summary>
    To = AVHWFrameTransferDirection.AV_HWFRAME_TRANSFER_DIRECTION_TO,
}