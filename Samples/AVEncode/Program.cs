using System.Diagnostics;

using FFmpeg.Wrapper;

if (args.Length < 1) {
    Console.WriteLine("Usage: AVEncode <output path>");
    return;
}
using var muxer = new MediaMuxer(args[0]);

double frameRate = 24.0;

using var videoFrame = new VideoFrame(1280, 720, PixelFormats.YUV420P);
using var videoEnc = new VideoEncoder(CodecIds.H264, videoFrame.Format, frameRate, bitrate: 900_000);
videoEnc.SetOption("preset", "faster"); //libx264 specific

//Note that some audio encoders require specific frame formats, requiring use of `SwResampler` (see AVTranscode sample).
//There may also be frame size constraints, see `AudioEncoder.FrameSize`.
using var audioFrame = new AudioFrame(SampleFormats.FloatPlanar, 48000, 2, 1024) { PresentationTimestamp = 0 };
using var audioEnc = new AudioEncoder(CodecIds.AAC, audioFrame.Format, bitrate: 128_000);

var videoStream = muxer.AddStream(videoEnc);
var audioStream = muxer.AddStream(audioEnc);
muxer.Open(); //Open encoders and write header

int numFrames = (int)(frameRate * 10 + 1); //encode 10s of video
for (int i = 0; i < numFrames; i++) {
    videoFrame.PresentationTimestamp = videoEnc.GetFramePts(frameNumber: i);
    GenerateFrame(videoFrame);
    muxer.EncodeAndWrite(videoStream, videoEnc, videoFrame);

    long samplePos = (long)(i / frameRate * audioEnc.SampleRate);
    while (audioFrame.PresentationTimestamp < samplePos) {
        GenerateAudio(audioFrame);
        muxer.EncodeAndWrite(audioStream, audioEnc, audioFrame);
        audioFrame.PresentationTimestamp += audioFrame.Count;
    }
}
//Flush delayed frames in the encoder
muxer.EncodeAndWrite(videoStream, videoEnc, null);

static void GenerateFrame(VideoFrame frame)
{
    Debug.Assert(frame.PixelFormat == PixelFormats.YUV420P);
    int ts = (int)frame.PresentationTimestamp!.Value;

    for (int y = 0; y < frame.Height; y++) {
        var rowY = frame.GetRowSpan<byte>(y, 0);

        for (int x = 0; x < frame.Width; x++) {
            rowY[x] = (byte)(x + y + ts * 3);
        }
    }
    for (int y = 0; y < frame.Height / 2; y++) {
        var rowU = frame.GetRowSpan<byte>(y, 1);
        var rowV = frame.GetRowSpan<byte>(y, 2);

        for (int x = 0; x < frame.Width / 2; x++) {
            rowU[x] = (byte)(128 + y + ts * 2);
            rowV[x] = (byte)(64 + x + ts * 5);
        }
    }
}
static void GenerateAudio(AudioFrame frame)
{
    Debug.Assert(frame.SampleFormat == SampleFormats.FloatPlanar && frame.NumChannels == 2);
    int samplePos = (int)frame.PresentationTimestamp!.Value;

    var samplesL = frame.GetChannelSamples<float>(0);
    var samplesR = frame.GetChannelSamples<float>(1);

    for (int i = 0; i < frame.Count; i++) {
        float a = MathF.Sin((samplePos + i) * (MathF.Tau * 440.0f / frame.SampleRate)) * 0.1f;
        samplesL[i] = a;
        samplesR[i] = a;
    }
}