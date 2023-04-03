namespace FFmpeg.Wrapper;

public unsafe class VideoFrame : MediaFrame
{
    public int Width => _frame->width;
    public int Height => _frame->height;
    public AVPixelFormat PixelFormat => (AVPixelFormat)_frame->format;

    public PictureFormat Format => new PictureFormat(Width, Height, PixelFormat);

    /// <summary> Pointer to the pixel planes. </summary>
    public byte** Data => (byte**)&_frame->data;

    /// <summary> Line size for each plane. </summary>
    public int* Strides => (int*)&_frame->linesize;

    public bool IsHardwareFormat => (ffmpeg.av_pix_fmt_desc_get(PixelFormat)->flags & ffmpeg.AV_PIX_FMT_FLAG_HWACCEL) != 0;

    /// <summary> Allocates a new empty <see cref="AVFrame"/>. </summary>
    public VideoFrame()
        : this(ffmpeg.av_frame_alloc(), clearToBlack: false, takeOwnership: true) { }
    public VideoFrame(PictureFormat fmt, bool clearToBlack = true)
        : this(fmt.Width, fmt.Height, fmt.PixelFormat, clearToBlack) { }

    public VideoFrame(int width, int height, AVPixelFormat fmt = AVPixelFormat.AV_PIX_FMT_RGBA, bool clearToBlack = true)
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
    public VideoFrame(AVFrame* frame, bool clearToBlack = false, bool takeOwnership = false)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
        _ownsFrame = takeOwnership;

        if (clearToBlack) {
            Clear();
        }
    }

    public Span<T> GetRowSpan<T>(int y, int plane = 0) where T : unmanaged
    {
        ThrowIfDisposed();

        //TODO: factor-in chroma height scale
        if ((uint)y >= (uint)Height || (uint)plane >= 4) {
            throw new ArgumentOutOfRangeException();
        }
        int stride = Strides[plane];
        return new Span<T>(&Data[plane][y * stride], stride / sizeof(T));
    }

    public HWFrameMapping Map(HWFrameMapFlags flags)
    {
        ThrowIfDisposed();

        var mappedFrame = ffmpeg.av_frame_alloc();
        int result = ffmpeg.av_hwframe_map(mappedFrame, _frame, (int)flags);

        if (result == 0) {
            return new HWFrameMapping(this, mappedFrame);
        }
        ffmpeg.av_frame_free(&mappedFrame);
        throw result.ThrowError("Failed to create hardware frame mapping");
    }

    /// <summary> Uploads data to this hardware frame. </summary>
    public void TransferFrom(VideoFrame source)
    {
        ThrowIfDisposed();
        ffmpeg.av_hwframe_transfer_data(_frame, source.Handle, 0).CheckError("Failed to upload data to hardware frame");
    }
    /// <summary> Downloads data from this hardware frame. </summary>
    public void TransferTo(VideoFrame dest)
    {
        ThrowIfDisposed();
        ffmpeg.av_hwframe_transfer_data(dest.Handle, _frame, 0).CheckError("Failed to download data from hardware frame");
    }


    /// <summary> Fills this picture with black pixels. </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        var strides = new long_array4();

        for (uint i = 0; i < 4; i++) {
            strides[i] = _frame->linesize[i];
        }
        ffmpeg.av_image_fill_black(
            ref Unsafe.As<byte_ptrArray8, byte_ptrArray4>(ref _frame->data), strides,
            PixelFormat, _frame->color_range, _frame->width, _frame->height
        ).CheckError("Failed to clear frame.");
    }

    /// <summary> Saves this picture to the specified file. The format will be choosen based on the file extension. (Can be either JPG or PNG) </summary>
    /// <remarks> This is an unoptimized debug method. Production use is not recommended. </remarks>
    /// <param name="quality">JPEG: Quantization factor. PNG: ZLib compression level. 0-100</param>
    public void Save(string filename, int quality = 90, int outWidth = 0, int outHeight = 0)
    {
        ThrowIfDisposed();

        bool jpeg = filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

        var codec = jpeg ? AVCodecID.AV_CODEC_ID_MJPEG : AVCodecID.AV_CODEC_ID_PNG;
        var pixFmt = jpeg ? AVPixelFormat.AV_PIX_FMT_YUV444P : AVPixelFormat.AV_PIX_FMT_RGBA;

        if (outWidth <= 0) outWidth = Width;
        if (outHeight <= 0) outHeight = Height;

        using var encoder = new VideoEncoder(codec, new PictureFormat(outWidth, outHeight, pixFmt), 1, 10000);

        if (jpeg) {
            //1-31
            int q = 1 + (100 - quality) * 31 / 100;
            encoder.MaxQuantizer = q;
            encoder.MinQuantizer = q;
            encoder.Handle->color_range = AVColorRange.AVCOL_RANGE_JPEG;
        } else {
            //zlib compression (0-9)
            encoder.CompressionLevel = quality * 9 / 100;
        }
        encoder.Open();

        using var tempFrame = new VideoFrame(encoder.FrameFormat);
        using var sws = new SwScaler(Format, tempFrame.Format);
        sws.Convert(this, tempFrame);
        encoder.SendFrame(tempFrame);

        using var packet = new MediaPacket();
        encoder.ReceivePacket(packet);

        File.WriteAllBytes(filename, packet.Data.ToArray());
    }
}
public unsafe class HWFrameMapping : FFObject
{
    public VideoFrame SourceFrame { get; }
    private AVFrame* _mapping;

    public AVFrame* MappedFrame {
        get {
            ThrowIfDisposed();
            return _mapping;
        }
    }

    public AVPixelFormat PixelFormat => (AVPixelFormat)_mapping->format;

    public byte** Data => (byte**)&_mapping->data;
    public int* Strides => (int*)&_mapping->linesize;

    internal HWFrameMapping(VideoFrame srcFrame, AVFrame* mappedFrame)
    {
        SourceFrame = srcFrame;
        _mapping = mappedFrame;
    }

    public Span<T> GetPlaneSpan<T>(int plane) where T : unmanaged
    {
        //TODO: factor-in chroma height scale
        if ((uint)plane >= 4) {
            throw new ArgumentOutOfRangeException();
        }
        int stride = _mapping->linesize[(uint)plane];
        ulong_array4 planeSizes = new();
        long_array4 linesizes = new();

        for (uint i = 0; i < 4; i++) {
            linesizes[i] = _mapping->linesize[i];
        }
        ffmpeg.av_image_fill_plane_sizes(ref planeSizes, PixelFormat, SourceFrame.Height, in linesizes);
        return new Span<T>(Data[plane], (int)(planeSizes[(uint)plane] / (uint)sizeof(T)));
    }

    protected override void Free()
    {
        if (_mapping != null) {
            fixed (AVFrame** ppMapping = &_mapping) {
                ffmpeg.av_frame_free(ppMapping);
            }
        }
    }
    private void ThrowIfDisposed()
    {
        if (_mapping == null) {
            throw new ObjectDisposedException(nameof(HWFrameMapping));
        }
    }
}
/// <summary> Flags to apply to hardware frame memory mappings. </summary>
public enum HWFrameMapFlags
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