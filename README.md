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
- [Mini OpenGL player w/ hardware decoding](./Samples/GLPlayer/)
- [Hardware encoding](./Samples/HWEncode/ShaderRecWindow.cs)
- [Transcoding audio and video](./Samples/AVTranscode/Program.cs)
- [Building and rendering filter graphs](./Samples/Filtering/Program.cs)

On Windows, FFmpeg binaries must be manually copied to the build directory, or specified through `ffmpeg.RootPath` as [explained here](https://github.com/Ruslan-B/FFmpeg.AutoGen#usage).

# Introduction
Write-up on a few key FFmpeg concepts and the wrapper.

## Codecs and Containers
^TODO

## Frames and Formats
Uncompressed audio and video frames can be represented in numerous different ways. Video frames are defined by resolution, colorspace, and pixel format; while audio frames are defined by sample rate, channel layout, and sample format.

Some formats can differ in how channels are arranged in memory - in either _interleaved_ or _planar_ layouts. Interleaved (or packed) formats store the value for each channel immediately next to each other, while planar formats store each channel in a different array.

Planar formats can make the implementation of many codecs simpler and more efficient, as each channel can be easily processed independently.  
They also facilitate chroma subsampling, a scheme where chroma channels (U and V) are typically stored at a factor of the resolution of the luma channel (Y).

Audio formats are a lot simpler than video (but perhaps not as intuitive), as they always represents the same amplitude levels[^2] that are encoded in one of a few handful of sample formats.  


<table>
  <tr>
    <th>Interleaved</th>
    <th>Planar</th>
  </tr>
  <tr>
<td>

```csharp
var frame = new VideoFrame(1024, 1024, PixelFormats.RGBA);
var row = frame.GetRowSpan<byte>(y);

row[x * 4 + 0] = 64;  //R
row[x * 4 + 1] = 128; //G
row[x * 4 + 2] = 255; //B
row[x * 4 + 3] = 255; //A
```

</td>
<td>

```csharp
var frame = new VideoFrame(1024, 1024, PixelFormats.YUV444);
var rowY = frame.GetRowSpan<byte>(y, plane: 0);
var rowU = frame.GetRowSpan<byte>(y, plane: 1);
var rowV = frame.GetRowSpan<byte>(y, plane: 2);

rowY[x] = 255;
rowU[x] = 32;
rowV[x] = 255;
```

</td>
  </tr>
</table>

Since codecs generally only support a handful of formats, converting between them is an essential operation.  FFmpeg provides optimized software-based video scaling and format conversion via _libswscale_, which is exposed in the `SwScaler` wrapper. Likewise, _libswresample_ provides for audio conversion, resampling, and mixing, exposed similarly in `SwResampler`.

## Timestamps and Time Bases
Because streams can have arbitrary frame and sample rates, representing frame timestamps with floating-point numbers or around a single scale could presumably lead to slight rounding errors that may accumulate over time. Instead, they are represented as fixed-point numbers based around an arbitrary _time base_ - a rational number denoting one second.

For video, the time base is normally set to `1/FrameRate`, and for audio, it is often `1/SampleRate`. When decoding something, it's best not to make any assumptions and properly convert between bases if necessary.  
The encoder wrappers will automatically set these values in the constructor, so they only need be changed when encoding variable frame-rate video or if required by the codec.

Converting between time bases is still often necessary for ease of interpretation. This can be done via the `Rational.Rescale()` (av_rescale_q) function, or one of the few other `TimeSpan` helpers: `Rational.GetTimeSpan()`, `MediaStream.GetTimestamp()` and `MediaEncoder.GetFramePts()`.

^TODO: PTS/DTS/BestEffortTimestamps

## Hardware Acceleration
Many video-related tasks can be offloaded to dedicated hardware for better performance and power efficiency. FFmpeg provides support for several platform specific APIs, that can be used for encoding, decoding, and filtering[^1].

The bulk of work in enabling hardware accelerated encoding/decoding typically involves the enumeration of possible hardware configurations (which may or not be supported by the platform), and trying to instantiate appliable devices. Once a valid device is found, the codec setup is fairly simple:

<table>
  <tr>
    <th>Decoding</th>
    <th>Encoding</th>
  </tr>
  <tr>
<td>

```csharp
var decoder = (VideoDecoder)demuxer.CreateStreamDecoder(stream, open: false);
var config = decoder.GetHardwareConfigs()
                    .FirstOrDefault(c => c.DeviceType == HWDeviceTypes.DXVA2);
using var device = HardwareDevice.Create(config.DeviceType);

if (device != null) {
    decoder.SetupHardwareAccelerator(config, device);
}
decoder.Open();
```

</td>
<td>

```csharp
var format = new PictureFormat(1920, 1080, PixelFormats.NV12);
using var device = VideoEncoder.CreateCompatibleHardwareDevice(CodecIds.HEVC, format, out var config);

if (device != null) {
    encoder = new VideoEncoder(config, format, frameRate: 30, device);
} else {
    //Software fallback
    encoder = new VideoEncoder(MediaCodec.GetEncoder("libx265"), format, frameRate: 30);
}
```

</td>
  </tr>
</table>

For decoding, `SetupHardwareAccelerator()` setups a negotiation callback via `AVCodecContext.get_format`, which can still reject the device if e.g. the codec settings aren't supported. In that case, it will silently fallback to software decoding.


Hardware frames need special handling as they generally refer to data outside main memory. Data needs to be copied via transfer calls or memory mappings (rarely supported).

```cs
using var decodedFrame = new VideoFrame();
using var swFrame = new VideoFrame();

while (decoder.ReceiveFrame(decodedFrame)) {
    if (decodedFrame.IsHardwareFrame) {
        decodedFrame.TransferTo(swFrame);
        //Use `swFrame`
    } else {
        //Use `decodedFrame`
    }
}
```

Most encoders can take normal software frames without any additional ceremony, but when that is not possible, hardware frames must be allocated via `HardwareFramePool`.

## Filters

^TODO?

## GC and Unmanaged Memory Lifetimes
Most wrappers use GC finalizers to free unmanaged memory in case `Dispose()` fails to be called. When accessing unmanaged properties from the wrappers (e.g. `Handle` properties, and span-returning methods such as `Frame.GetSpan()`), users should ensure that wrapper objects are not collected by the GC while unmanaged memory is in use, otherwise it could be freed by the finalizer.  
This can be done by keeping external references (in a field), wrapping in `using` blocks, or calling `GC.KeepAlive()` after the code accessing unmanged memory.

[^1]: https://trac.ffmpeg.org/wiki/HWAccelIntro
[^2]: https://developer.mozilla.org/en-US/docs/Web/Media/Formats/Audio_concepts
[^3]: https://developer.mozilla.org/en-US/docs/Web/Media/Formats/Video_concepts