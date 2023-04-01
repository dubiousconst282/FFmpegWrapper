# FFmpegWrapper
Object-oriented FFmpeg API wrappers (powered by `FFmpeg.AutoGen`).

---

Using the ffmpeg API tends to be tedious and error prone, mainly due to the extensive need for struct setup and manual error checking.  
This library aims to abstract away most of such code while still exposing pointers in order to allow for lower-level control.

## Examples
See the [samples](./Samples/) directory for full code samples.

- [Thumbnail Extractor (video decode, seek)](./Samples/ThumbExtractor/Program.cs)
- [Encoding procedural audio and video](./Samples/AVEncode/Program.cs)
- [Encoding SkiaSharp bitmaps (swscaler color conversion)](./Samples/SkiaInterop/Program.cs)
- [Hardware decoding and toy OpenGL player](./Samples/HWDecode/VideoPlayerWindow.cs)

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
    frame.PresentationTimestamp = ffmpeg.av_rescale_q(i, ffmpeg.av_inv_q(videoEnc.FrameRate), videoEnc.TimeBase);
    GenerateFrame(frame); //Fill `frame` with something interesting...
    muxer.EncodeAndWrite(stream, encoder, frame); //all-in-one: send_frame(), receive_packet(), rescale_ts(), write_interleaved()
}
//Flush delayed frames in the encoder
muxer.EncodeAndWrite(stream, encoder, null);
```