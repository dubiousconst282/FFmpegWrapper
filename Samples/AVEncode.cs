using FFmpegWrapper;
using FFmpegWrapper.Codec;
using FFmpegWrapper.Container;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Samples
{
    public class AVEncode
    {
        private MediaMuxer muxer;

        private VideoEncoder videoEnc;
        private AudioEncoder audioEnc;

        private MediaStream videoStream;
        private MediaStream audioStream;

        private Picture picture;
        private AudioFrame audioFrame;
        private int audioFrameSize;

        private MediaPacket packet = new MediaPacket();

        public void Init(string filename)
        {
            muxer = new MediaMuxer(filename);

            picture = new Picture(854, 480, PixelFormats.YUV420P);
            audioFrame = new AudioFrame(48000, 2, SampleFormats.FloatPlanar, 1024);
            
            //"ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow", "placebo"
            videoEnc = new VideoEncoder(CodecIds.H264, picture.Info, 23.976, 768 * 1024);
            videoEnc.SetOption("preset", "faster");

            audioEnc = new AudioEncoder(CodecIds.AAC, audioFrame.Format, 128 * 1024);

            videoStream = muxer.AddStream(videoEnc);
            audioStream = muxer.AddStream(audioEnc);

            videoEnc.Open();
            audioEnc.Open();

            muxer.Open();

            audioFrameSize = audioEnc.FrameSize;
        }

        public void WriteFrames(double durationMs)
        {
            double curr = 0;
            int audioFrameNo = 0;
            int videoFrameNo = 0;

            double lastVideoFrame = double.MinValue;
            double lastAudioFrame = double.MinValue;

            double videoInterval = 1000 / videoEnc.FrameRate.ToDouble();
            double audioInterval = 1000 / (48000.0 / audioFrameSize);
            while (curr < durationMs) {
                if (curr - lastVideoFrame >= videoInterval) {
                    FillVideoFrame(videoFrameNo);

                    videoEnc.SendFrame(picture, videoFrameNo);
                    while (videoEnc.ReceivePacket(packet).IsSuccess()) {
                        muxer.Write(videoStream, packet);
                    }

                    lastVideoFrame = curr;
                    videoFrameNo++;
                }
                if (curr - lastAudioFrame >= audioInterval) {
                    FillAudioFrame(audioFrameNo);

                    audioEnc.SendFrame(audioFrame, audioFrameNo * audioFrameSize);
                    while (audioEnc.ReceivePacket(packet).IsSuccess()) {
                        muxer.Write(audioStream, packet);
                    }

                    lastAudioFrame = curr;
                    audioFrameNo++;
                }
                double t = Math.Min((lastAudioFrame + audioInterval) - curr, 
                                    (lastVideoFrame + videoInterval) - curr);
                curr += Math.Max(0.001, t);
            }
            Flush();
        }

        private void Flush()
        {
            videoEnc.SendFrame(null, 0);
            while (videoEnc.ReceivePacket(packet).IsSuccess()) {
                muxer.Write(videoStream, packet);
            }

            audioEnc.SendFrame(null, 0);
            while (audioEnc.ReceivePacket(packet).IsSuccess()) {
                muxer.Write(audioStream, packet);
            }
        }

        public void End()
        {
            picture.Dispose();
            audioFrame.Dispose();

            videoEnc.Dispose();
            audioEnc.Dispose();

            muxer.Dispose();
        }

        private unsafe void FillVideoFrame(int no)
        {
            int width = picture.Width;
            int height = picture.Height;

            byte* Y = picture.Planes[0];
            byte* U = picture.Planes[1];
            byte* V = picture.Planes[2];

            int Ys = picture.Strides[0];
            int Us = picture.Strides[1];
            int Vs = picture.Strides[2];

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    Y[x + y * Ys] = (byte)(x + y + no * 3);
                    U[(x / 2) + (y / 2) * Us] = (byte)(128 + y + no * 2);
                    V[(x / 2) + (y / 2) * Vs] = (byte)(64 + x + no * 5);
                }
            }
        }
        private unsafe void FillAudioFrame(int no)
        {
            int channels = audioFrame.Channels;
            float rate = audioFrame.SampleRate;

            audioFrame.Count = audioFrameSize;

            for (int i = 0; i < audioFrameSize; i++) {
                float amp = MathF.Sin(MathF.PI * 2 * 440 * ((no * audioFrameSize) + i) / rate);
                for (int j = 0; j < channels; j++) {
                    audioFrame.SetSampleFloat(i, j, amp);
                }
            }
        }
    }
}
