# FFmpeg.ApiWrapper
![GitHub](https://img.shields.io/github/license/dubiousconst282/FFmpegWrapper)
[![Nuget](https://img.shields.io/nuget/v/FFmpeg.ApiWrapper)](https://www.nuget.org/packages/FFmpeg.ApiWrapper)

Low level, mostly safe FFmpeg API wrappers built on top of [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen).

---

Using the FFmpeg API tends to be tedious and error prone due to the amount of struct setup, memory management, and vague error codes.  
This library aims to provide safe idiomatic wrappers and utility functions around several APIs, to facilitate development of media applications.

## Examples
The easiest way to get started may be by looking at the code samples listed below. The wrappers do not diverge too much from their native counterparts, so familiarity with the native API may help with more complicated use cases.

- [Extracting video frames](./Samples/FrameExtractor/Program.cs)
- [Encoding generated audio and video](./Samples/AVEncode/Program.cs)
- [Encoding SkiaSharp bitmaps (swscaler color conversion)](./Samples/SkiaInterop/Program.cs)
- [Hardware decoding and crude OpenGL player](./Samples/HWDecode/PlayerWindow.cs)
- [Hardware encoding](./Samples/HWEncode/ShaderRecWindow.cs)
- [Transcoding audio and video](./Samples/AVTranscode/Program.cs)
- [Building and rendering filter graphs](./Samples/Filtering/Program.cs)

On Windows, FFmpeg binaries must be manually copied to the build directory, or specified through `ffmpeg.RootPath` as [explained here](https://github.com/Ruslan-B/FFmpeg.AutoGen#usage).
