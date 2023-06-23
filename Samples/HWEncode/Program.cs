if (args.Length < 1) {
    Console.WriteLine("Usage: HWEncode <output video path>");
    return;
}
new ShaderRecWindow(args[0]).Run();