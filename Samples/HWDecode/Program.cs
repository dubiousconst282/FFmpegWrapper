if (args.Length < 1) {
    Console.WriteLine("Usage: HWDecode <input video path>");
    return;
}
new VideoPlayerWindow(args[0]).Run();