# FFmpegWrapper
Object-oriented FFmpeg API wrappers (powered by `FFmpeg.AutoGen`).

![GitHub](https://img.shields.io/github/license/dubiousconst282/FFmpegWrapper)
[![Nuget](https://img.shields.io/nuget/v/FFmpeg.ApiWrapper)](https://www.nuget.org/packages/FFmpeg.ApiWrapper)

---

Using the ffmpeg API tends to be tedious and error prone, mainly due to the extensive need for struct setup and manual error checking.  
This library aims to abstract away most of such code while still exposing pointers in order to allow for lower-level control.

## Examples
Executable code samples are available in the [samples](./Samples/) directory.

- [Thumbnail Extractor (video decode, seek)](./Samples/ThumbExtractor/Program.cs)
- [Encoding procedural audio and video](./Samples/AVEncode/Program.cs)
- [Encoding SkiaSharp bitmaps (swscaler color conversion)](./Samples/SkiaInterop/Program.cs)
- [Hardware decoding and toy OpenGL player](./Samples/HWDecode/VideoPlayerWindow.cs)
- [Hardware encoding](./Samples/HWEncode/PlaybackWindow.cs)

### Showcase: Basic video encoding
```cs
using var muxer = new MediaMuxer("output.mp4");

using var frame = new VideoFrame(1280, 720, PixelFormats.YUV420P);
using var encoder = new VideoEncoder(CodecIds.H264, frame.Format, frameRate: 24.0, bitrate: 900_000);
encoder.SetOption("preset", "faster"); //libx264 specific
encoder.Open();

var stream = muxer.AddStream(encoder);
muxer.Open();

for (int i = 0; i < 24 * 10; i++) { //Encode 10s of video
    frame.PresentationTimestamp = encoder.GetFramePts(frameNumber: i); //Based on framerate. Alt overload takes TimeSpan.
    // ... fill `frame` with something interesting ...
    muxer.EncodeAndWrite(stream, encoder, frame); //all-in-one: send_frame(), receive_packet(), rescale_ts(), write_interleaved()
}
muxer.EncodeAndWrite(stream, encoder, null); //flush delayed frames in the encoder
```
