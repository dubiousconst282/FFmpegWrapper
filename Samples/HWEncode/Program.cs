if (args.Length < 1) {
    Console.WriteLine("Usage: HWEncode <output video path>");
    return;
}
new PlaybackWindow(args[0]).Run();