using FFmpeg.AutoGen;
using FFmpegWrapper;
using FFmpegWrapper.Codec;
using FFmpegWrapper.Container;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Samples
{
    public class ConsoleVideoPlayer : IDisposable
    {
        private MediaDemuxer _demuxer;
        private MediaStream _stream;
        private VideoDecoder _decoder;
        private SwScaler _sws;
        private Picture _srcPic; //decoded picture
        private Picture _dstPic; //rescaled picture

        public ConsoleVideoPlayer(string filename)
        {
            //Setup the demuxer and decoder.
            _demuxer = new MediaDemuxer(filename);
            _stream = _demuxer.FindStream(MediaType.Video);

            _decoder = (VideoDecoder)_stream.OpenDecoder();

            //Setup the frames and the rescaler.
            int width = Console.WindowWidth - 1;
            int height = Console.WindowHeight - 1;

            _srcPic = _decoder.AllocateFrame();
            _dstPic = new Picture(width, height, PixelFormats.RGBA);
            _sws = new SwScaler(_srcPic.Info, _dstPic.Info);
        }

        public void Run()
        {
            var pkt = new MediaPacket();
            
            //Loop until the user press a key or there is no more frames.
            while (!Console.KeyAvailable && _demuxer.Read(pkt).IsSuccess()) {
                //Only process packets from the stream we selected on the ctor.
                if (pkt.StreamIndex != _stream.Index) continue;

                //Decode
                _decoder.SendPacket(pkt);
                while (_decoder.ReceiveFrame(_srcPic, out long timestamp).IsSuccess()) {
                    //Rescale & display
                    _sws.Scale(_srcPic, _dstPic);
                    DisplayFrame(_dstPic);
                }
            }
        }

        private unsafe void DisplayFrame(Picture pic)
        {
            int w = pic.Width;
            int h = pic.Height;

            var sb = new StringBuilder(w * h * 20);

            //Loop through the rescaled frame pixels.
            //The sb is filled with ANSI color codes. (unfortunately, the console is slow to handle it.)
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    Pixel px = pic.GetPixel(x, y);
                    const int S = 4; // full 8-bit rendering seems to be slower...
                    int r = px.R >> S << S;
                    int g = px.G >> S << S;
                    int b = px.B >> S << S;

                    //https://stackoverflow.com/a/33206814
                    sb.AppendFormat("\u001b[48;2;{0};{1};{2}m ", r, g, b);
                }
                sb.Append('\n');
            }
            Console.CursorVisible = false;
            Console.SetCursorPosition(0, 0);
            Console.Write(sb.ToString());
        }
        
        public void Dispose()
        {
            //Free all unmanaged resources..
            _srcPic.Dispose();
            _dstPic.Dispose();
            _sws.Dispose();

            _decoder.Dispose();
            _demuxer.Dispose();
        }
    }
}
