using Tesseract;

// Separate local worker gives batch OCR a cancellable process boundary and supports
// both x64 Windows and Windows ARM64's x64 emulation without package-identity APIs.
if(args.Length!=2 || !Path.IsPathFullyQualified(args[0]) || !Path.IsPathFullyQualified(args[1])) return 2;
try
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    using var engine=new TesseractEngine(Path.Combine(AppContext.BaseDirectory,"tessdata"),"eng",EngineMode.Default);
    using var image=Pix.LoadFromFile(args[0]);
    using var page=engine.Process(image);
    File.WriteAllText(args[1],page.GetText());
    return 0;
}
catch(Exception error)
{
    Console.Error.WriteLine(error is DllNotFoundException || error.InnerException is DllNotFoundException
        ? "The Microsoft Visual C++ 2015–2022 x64 runtime is required for local text extraction. Install it from Microsoft's official download page."
        : "Text extraction failed. The image may be unsupported, damaged, or too large.");
    return 1;
}
