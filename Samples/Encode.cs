using FFmpegWrapper;
using FFmpegWrapper.Codec;
using FFmpegWrapper.Container;
using System;
using System.Collections.Generic;
using System.Text;

namespace Samples
{
    public class Encode
    {
        public static void EncodeVideo(string filename)
        {
            using (var encoder = new VideoEncoder(CodecIds.H264, 854, 480, PixelFormats.YUV444P, 23.976, 1024 * 1024))
            using (var muxer = new MediaMuxer(filename))
            using (var pic = new Picture(encoder.Info)) {
                encoder.SetOption("preset", "veryfast"); //x264 specific
                //encoder.SetOption("tune", "zerolatency");

                var stream = muxer.AddStream(encoder);

                encoder.Open();
                muxer.Open();
                var pkt = new MediaPacket();

                for (int i = 0; i < 5 * 24; i++) {
                    FillFrame(pic, i);
                    encoder.SendFrame(pic, i + 1);
                    while (encoder.ReceivePacket(pkt).IsSuccess()) {
                        muxer.Write(stream, pkt);
                    }
                }

                encoder.SendFrame(null, 0);
                while (encoder.ReceivePacket(pkt).IsSuccess()) {
                    muxer.Write(stream, pkt);
                }
            }
        }
        private static unsafe void FillFrame(Picture pic, int ts)
        {
            int width = pic.Width;
            int height = pic.Height;

            int Ys = pic.Strides[0];
            int Us = pic.Strides[1];
            int Vs = pic.Strides[2];

            for (int y = 0; y < height; y++) {
                byte* Y = pic.Planes[0] + y * Ys;
                byte* U = pic.Planes[1] + y * Us;
                byte* V = pic.Planes[2] + y * Vs;

                for (int x = 0; x < width; x++) {
                    Y[x] = (byte)(x + y + ts * 3);
                    U[x] = (byte)(128 + y + ts * 2);
                    V[x] = (byte)(64 + x + ts * 5);
                }
            }
        }

        public static unsafe void EncodeAudio(string filename)
        {
            const int FRAME_SIZE = 1024;
            const int SAMPLE_RATE = 44100;
            const int CHANNELS = 2;

            int frameCount = 8 * SAMPLE_RATE / FRAME_SIZE; //encode 8 seconds of audio

            using (var muxer = new MediaMuxer(filename))
            using (var encoder = new AudioEncoder(CodecIds.MP3, SAMPLE_RATE, CHANNELS, SampleFormats.S16Planar, 192000))
            using (var frame = new AudioFrame(encoder.AudioFormat, FRAME_SIZE)) {
                var stream = muxer.AddStream(encoder);

                encoder.Open();
                muxer.Open(); //Open the muxer once we set it up.

                frame.Count = FRAME_SIZE;

                var pkt = new MediaPacket();

                for (int i = 0; i < frameCount; i++) {
                    //Fill the frame
                    for (int j = 0; j < FRAME_SIZE; j++) {
                        int t = i * FRAME_SIZE + j;
                        int t8khz = (int)(t * 8000L / SAMPLE_RATE);
                        short s = (short)(Rick8bit(t8khz) << 5);

                        frame.SetSampleShort(j, 0, s);
                        frame.SetSampleShort(j, 1, s);
                    }
                    //Encode
                    encoder.SendFrame(frame, i * FRAME_SIZE);
                    while (encoder.ReceivePacket(pkt).IsSuccess()) {
                        muxer.Write(stream, pkt);
                    }
                }

                //Flush the encoder
                encoder.SendFrame(null, 0);
                while (encoder.ReceivePacket(pkt).IsSuccess()) {
                    muxer.Write(stream, pkt);
                }
            }
        }

        private static double[] T = { 8 / 9.0, 1.0, 9 / 8.0, 6 / 5.0, 4 / 3.0, 3 / 2.0, 0.0 }; //Tone/Frequency?
        private static int[] N = { 0xd2d2c8, 0xce4088, 0xca32c8, 0x8e4009 }; //Note indices?
        private static byte Rick8bit(int t) //"Never gonna give you up"
        {
            //http://wurstcaptures.untergrund.net/music/
            //https://github.com/feilipu/avrfreertos/blob/master/audio_shield/music_formula_collection.txt

            // gasman 2011-10-05 http://pouet.net/topic.php?which=8357&page=12 js-only
            //(t<<3)*[8/9,1,9/8,6/5,4/3,3/2,0][[0xd2d2c8,0xce4088,0xca32c8,0x8e4009][t>>14&3]>>(0x3dbe4688>>((t>>10&15)>9?18:t>>10&15)*3&7)*3&7]

            double amp = (t << 3) * T[N[t >> 14 & 3] >> (0x3dbe4688 >> ((t >> 10 & 15) > 9 ? 18 : t >> 10 & 15) * 3 & 7) * 3 & 7];

            return (byte)amp;
        }
    }
}
