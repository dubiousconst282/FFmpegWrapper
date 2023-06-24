using FFmpeg.Wrapper;

//Create a filter graph equivalent to the following command:
//ffplay -f lavfi -i ddagrab=draw_mouse=1:framerate=60,hwdownload,format=bgra,scale,format=yuv444p,mestimate=method=epzs,codecview=mvt=fp

using var graph = new MediaFilterGraph();

using var device = HardwareDevice.Create(HWDeviceTypes.D3D11VA);

var sourceNode = 
    graph.AddNode(new() {
        FilterName = "ddagrab",
        Arguments = {
            ("draw_mouse", 1),
            ("framerate", 60)
        },
        HardwareDevice = device
    });

var downloadNode = 
    graph.AddNode(new() {
        FilterName = "hwdownload", 
        Inputs = { sourceNode.GetOutput(0) }
    });

//Note that "format" nodes are somewhat misleading, they will _not_ convert pixel formats, only ensure that
//it output matches the specified format iff the input node also supports it.
var convertNode =
    graph.AddNode(new() {
        FilterName = "format",
        Inputs = { downloadNode.GetOutput(0) },
        Arguments = { ("pix_fmts", "bgra") }
    });

var scaleNode =
    graph.AddNode(new() {
        FilterName = "scale",
        Inputs = { convertNode.GetOutput(0) },
        Arguments = { ("w", "iw*0.8"), ("h","ih*0.8") }
    });
var convertNode2 =
    graph.AddNode(new() {
        FilterName = "format",
        Inputs = { scaleNode.GetOutput(0) },
        Arguments = { ("pix_fmts", "yuv444p") }
    });

var mestimateNode =
    graph.AddNode(new() {
        FilterName = "mestimate",
        Inputs = { convertNode2.GetOutput(0) },
        Arguments = { ("method", "epzs"), ("mb_size", 32), ("search_param", 15) }
    });
var codecviewNode =
    graph.AddNode(new() {
        FilterName = "codecview",
        Inputs = { mestimateNode.GetOutput(0) },
        Arguments = { ("mv_type", "fp") }
    });


var sinkNode = graph.AddVideoBufferSink(codecviewNode.GetOutput(0));

graph.Configure();

using var muxer = new MediaMuxer("screencap.mp4");
using var encoder = new VideoEncoder(CodecIds.H264, sinkNode.Format, sinkNode.FrameRate);
encoder.TimeBase = sinkNode.TimeBase;

encoder.SetOption("crf", "21");
encoder.SetOption("preset", "fast");
encoder.SetOption("rc-lookahead", "5");

var stream = muxer.AddStream(encoder);

muxer.Open();

while (true) {
    using var frame = sinkNode.ReceiveFrame() ?? throw new Exception("Could not get frame from filter sink");
    
    muxer.EncodeAndWrite(stream, encoder, frame);

    var timestamp = Rational.GetTimeSpan(frame.PresentationTimestamp!.Value, encoder.TimeBase);
    Console.WriteLine(timestamp);
    if (timestamp.TotalSeconds >= 15) break;
}
//Flush frames delayed in the encoder
muxer.EncodeAndWrite(stream, encoder, null);

