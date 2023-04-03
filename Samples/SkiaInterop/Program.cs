using SkiaSharp;

using FFmpeg.Wrapper;

if (args.Length < 1) {
    Console.WriteLine("Usage: SkiaInterop <output path>");
    return;
}
using var muxer = new MediaMuxer(args[0]);

double frameRate = 30.0;

using var encoder = new VideoEncoder(CodecIds.H264, new PictureFormat(1280, 720, PixelFormats.YUV420P), frameRate, bitrate: 1200_000);
using var frame = new VideoFrame(encoder.FrameFormat);
encoder.Open();

var stream = muxer.AddStream(encoder);
muxer.Open();

using var bitmap = new SKBitmap(frame.Width, frame.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
using var canvas = new SKCanvas(bitmap);
using var scaler = new SwScaler(new PictureFormat(bitmap.Width, bitmap.Height, PixelFormats.RGBA), frame.Format, InterpolationMode.Bilinear);

int numFrames = (int)(frameRate * 10 + 1); //encode 10s of video
for (int i = 0; i < numFrames; i++) {
    Console.Write($"Generating frame {i}/{numFrames}\r");

    //Draw some weird stuff
    using var paint = new SKPaint() {
        IsAntialias = true,
        Color = SKColors.Black,
        TextAlign = SKTextAlign.Right,
        TextSize = 48
    };
    canvas.Clear(SKColors.White);
    canvas.DrawText("Frame #" + i, bitmap.Width - 4, paint.TextSize + 4, paint);

    paint.ImageFilter = SKImageFilter.CreateDropShadow(2f, 2f, 4f, 4f, 0x70_000000);

    for (int j = 0; j < 40; j++) {
        float t = i / (float)frameRate * 0.8f + j / 40.0f;
        float x = j / 40.0f * bitmap.Width;
        float y = bitmap.Height * 0.7f + MathF.Sin(t * 5) * 150;

        paint.Color = SKColor.FromHsv((t * 200) % 360, 75, 90);
        canvas.DrawCircle(x, y, 32, paint);
    }
    paint.ImageFilter = null;

    paint.Shader = SKShader.CreatePerlinNoiseImprovedNoise(0.03f, 0.03f, 3, i / (float)frameRate * 1.3f);
    canvas.DrawRect(32, 32, 256, 256, paint);

    //Convert to YUV and encode
    canvas.Flush();
    scaler.Convert(bitmap.GetPixelSpan(), bitmap.RowBytes, frame);

    frame.PresentationTimestamp = encoder.GetFramePts(frameNumber: i);
    muxer.EncodeAndWrite(stream, encoder, frame);
}
//Flush delayed frames in the encoder
muxer.EncodeAndWrite(stream, encoder, null!);