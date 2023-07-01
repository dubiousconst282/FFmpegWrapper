if (args.Length < 1) {
    Console.WriteLine("Usage: GLPlayer <input video path>");
    return;
}
new PlayerWindow(args[0]).Run();