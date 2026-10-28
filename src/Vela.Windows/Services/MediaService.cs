using Windows.Graphics.Imaging;
using Windows.Foundation;
using System.Diagnostics;
using Vela.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;
using Windows.Storage.Streams;

namespace Vela.Windows.Services;

internal sealed record ImageConversionResult(int Completed,int Total,IReadOnlyList<string> Failures)
{
    public bool HasFailures=>Failures.Count>0;
    public string Message=>$"Converted {Completed} of {Total} images. Originals were kept."
        +(HasFailures?"\n"+string.Join("\n",Failures.Take(8)):"");
}

internal sealed record OcrBatchResult(int Completed,int Total,IReadOnlyList<string> Failures)
{
    public bool HasFailures=>Failures.Count>0;
    public string Message=>$"Saved text from {Completed} of {Total} images. English text extraction runs offline. Originals were kept."
        +(HasFailures?"\n"+string.Join("\n",Failures.Take(8)):"")
        +(Failures.Count>8?$"\n{Failures.Count-8} more image(s) failed.":"");
}

internal static class MediaService
{
    public static async Task<string> SaveClipboardImage(nint hwnd,CancellationToken cancellation=default)
    {
        var content=Clipboard.GetContent();
        if(!content.Contains(StandardDataFormats.Bitmap)) return "Copy an image first, then try again.";
        var picker=new FileSavePicker { SuggestedFileName="Clipboard image" };
        picker.FileTypeChoices.Add("PNG image",[".png"]); InitializeWithWindow.Initialize(picker,hwnd);
        var file=await picker.PickSaveFileAsync().AsTask(cancellation); if(file is null) return "Save canceled.";
        cancellation.ThrowIfCancellationRequested();
        using var source=await (await content.GetBitmapAsync().AsTask(cancellation)).OpenReadAsync().AsTask(cancellation);
        await SavePngCopy(source,file.Path,cancellation);
        return $"Saved {file.Name}.";
    }
    internal static async Task SavePngCopy(IRandomAccessStream source,string destination,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        var decoder=await BitmapDecoder.CreateAsync(source).AsTask(cancellation);
        if((long)decoder.PixelWidth*decoder.PixelHeight>100_000_000)
            throw new InvalidOperationException("That image exceeds the 100-megapixel processing limit.");
        using var bitmap=await decoder.GetSoftwareBitmapAsync().AsTask(cancellation);
        await AtomicFileOutput.Write(destination,async (temporary,token)=>
        {
            var stage=await StorageFile.GetFileFromPathAsync(temporary).AsTask(token);
            using var output=await stage.OpenAsync(FileAccessMode.ReadWrite).AsTask(token);
            await Encode(bitmap,output,BitmapEncoder.PngEncoderId,.9,token);
        },cancellation);
    }
    public static async Task<IReadOnlyList<StorageFile>> PickImages(nint hwnd)
    {
        var picker=new FileOpenPicker(); foreach(var extension in new[]{".png",".jpg",".jpeg",".bmp",".tif",".tiff",".webp"}) picker.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(picker,hwnd); return await picker.PickMultipleFilesAsync();
    }
    public static async Task<StorageFolder?> PickFolder(nint hwnd)
    {
        var picker=new FolderPicker(); picker.FileTypeFilter.Add("*"); InitializeWithWindow.Initialize(picker,hwnd);
        return await picker.PickSingleFolderAsync();
    }
    public static async Task<ImageConversionResult> Convert(IReadOnlyList<StorageFile> files,StorageFolder folder,string extension,Action<string> progress,CancellationToken cancellation,uint longestEdge=0,double quality=.9,ImageWatermark? watermark=null,long maximumBytes=0)
    {
        if(extension is not (".png" or ".jpg" or ".bmp")) throw new ArgumentOutOfRangeException(nameof(extension),"Choose PNG, JPEG or BMP output.");
        if(maximumBytes is <0 or >104857600) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if(!double.IsFinite(quality)) throw new ArgumentOutOfRangeException(nameof(quality));
        int completed=0; var failures=new List<string>();
        Guid format=extension switch { ".jpg"=>BitmapEncoder.JpegEncoderId,".bmp"=>BitmapEncoder.BmpEncoderId,_=>BitmapEncoder.PngEncoderId };
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            StorageFile? result=null;
            try
            {
                progress($"Converting {file.Name}…");
                using var source=await file.OpenReadAsync().AsTask(cancellation); var decoder=await BitmapDecoder.CreateAsync(source).AsTask(cancellation);
                if((long)decoder.PixelWidth*decoder.PixelHeight>100_000_000) throw new InvalidOperationException($"{file.Name} exceeds the 100-megapixel processing limit.");
                var size=ImageSizing.Fit(decoder.PixelWidth,decoder.PixelHeight,longestEdge);
                using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,extension==".png"?BitmapAlphaMode.Premultiplied:BitmapAlphaMode.Ignore,
                    new BitmapTransform { ScaledWidth=size.Width,ScaledHeight=size.Height,InterpolationMode=BitmapInterpolationMode.Fant },ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(cancellation);
                if(watermark is not null) await Task.Run(()=>ImageWatermarks.Apply(bitmap,watermark,cancellation),cancellation);
                double outputQuality=Math.Clamp(quality,.1,1); uint outputEdge=0;
                if(maximumBytes>0 && extension==".jpg")
                {
                    progress($"Choosing JPEG quality for {file.Name}…");
                    var choice=await JpegSizeBudget.Choose(maximumBytes,(int)Math.Round(outputQuality*100),async (candidate,token)=>
                    {
                        using var trial=new InMemoryRandomAccessStream();
                        await Encode(bitmap,trial,format,candidate/100d,token);
                        return checked((long)trial.Size);
                    },cancellation);
                    if(choice is null) throw new InvalidOperationException($"Vela could not fit {file.Name} within the JPEG size limit. Reduce its maximum edge or increase the size limit.");
                    outputQuality=choice.Value.Quality/100d;
                }
                else if(maximumBytes>0)
                {
                    progress($"Choosing {extension[1..].ToUpperInvariant()} dimensions for {file.Name}…");
                    var choice=await ImageSizeBudget.Choose(maximumBytes,(uint)Math.Max(bitmap.PixelWidth,bitmap.PixelHeight),async (candidate,token)=>
                    {
                        using var trial=new InMemoryRandomAccessStream();
                        await Encode(bitmap,trial,format,outputQuality,token,candidate);
                        return checked((long)trial.Size);
                    },cancellation);
                    if(choice is null) throw new InvalidOperationException($"{file.Name} could not fit within the {extension[1..].ToUpperInvariant()} size limit without reducing its long edge below {Math.Min(ImageSizeBudget.MinimumEdge,(uint)Math.Max(bitmap.PixelWidth,bitmap.PixelHeight))} pixels. Increase the limit.");
                    outputEdge=choice.Value.LongestEdge;
                }
                cancellation.ThrowIfCancellationRequested();
                result=await folder.CreateFileAsync(Path.GetFileNameWithoutExtension(file.Name)+extension,CreationCollisionOption.GenerateUniqueName);
                using var output=await result.OpenAsync(FileAccessMode.ReadWrite);
                progress($"Writing {file.Name}…");
                await Encode(bitmap,output,format,outputQuality,cancellation,outputEdge);
                if(maximumBytes>0 && output.Size>(ulong)maximumBytes) throw new InvalidOperationException($"{file.Name} could not be encoded within the {extension[1..].ToUpperInvariant()} size limit. No oversized copy was kept.");
                cancellation.ThrowIfCancellationRequested();
            }
            catch(OperationCanceledException) { if(result is not null) await result.DeleteAsync(); throw; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or System.Runtime.InteropServices.COMException)
            { if(result is not null) await result.DeleteAsync(); failures.Add($"{file.Name}: {ex.Message}"); continue; }
            completed++;
        }
        return new(completed,files.Count,failures.AsReadOnly());
    }
    private static async Task Encode(SoftwareBitmap bitmap,IRandomAccessStream output,Guid format,double quality,CancellationToken cancellation,uint longestEdge=0)
    {
        cancellation.ThrowIfCancellationRequested();
        var encoder=format==BitmapEncoder.JpegEncoderId
            ? await BitmapEncoder.CreateAsync(format,output,new BitmapPropertySet { ["ImageQuality"]=new((float)quality,PropertyType.Single) }).AsTask(cancellation)
            : await BitmapEncoder.CreateAsync(format,output).AsTask(cancellation);
        encoder.SetSoftwareBitmap(bitmap);
        if(longestEdge>0)
        {
            var size=ImageSizing.Fit((uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,longestEdge);
            encoder.BitmapTransform.ScaledWidth=size.Width; encoder.BitmapTransform.ScaledHeight=size.Height;
            encoder.BitmapTransform.InterpolationMode=BitmapInterpolationMode.Fant;
        }
        await encoder.FlushAsync().AsTask(cancellation);
        cancellation.ThrowIfCancellationRequested();
    }
    public static async Task<OcrBatchResult> Ocr(IReadOnlyList<StorageFile> files,StorageFolder folder,Action<string> progress,CancellationToken cancellation,
        Func<string,string,CancellationToken,Task>? recognize=null)
    {
        cancellation.ThrowIfCancellationRequested();
        if(files.Count==0) return new(0,0,[]);
        string worker=Path.Combine(AppContext.BaseDirectory,"Ocr","Vela.OcrWorker.exe");
        if(!File.Exists(worker)) throw new InvalidOperationException("The OCR worker is missing. Extract the complete Vela download before using text extraction.");
        int completed=0; var failures=new List<string>();
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            DirectoryInfo? temporary=null; StorageFile? result=null;
            try
            {
                progress($"Reading text in {file.Name}…"); cancellation.ThrowIfCancellationRequested();
                temporary=Directory.CreateTempSubdirectory("VelaOCR-");
                using var source=await file.OpenReadAsync().AsTask(cancellation);
                var decoder=await BitmapDecoder.CreateAsync(source).AsTask(cancellation);
                if((long)decoder.PixelWidth*decoder.PixelHeight>100_000_000) throw new InvalidOperationException($"{file.Name} exceeds the 100-megapixel processing limit.");
                var size=ImageSizing.Fit(decoder.PixelWidth,decoder.PixelHeight,4096);
                using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,
                    new BitmapTransform { ScaledWidth=size.Width,ScaledHeight=size.Height },ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(cancellation);
                var input=await StorageFile.GetFileFromPathAsync(CreateEmptyFile(Path.Combine(temporary.FullName,"input.png")));
                using(var stream=await input.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream).AsTask(cancellation);
                    encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync().AsTask(cancellation);
                }
                string textPath=Path.Combine(temporary.FullName,"text.txt");
                if(recognize is null)
                    await RunOcrWorker(worker,input.Path,textPath,()=>progress($"Extracting text in {file.Name}…"),cancellation);
                else await recognize(input.Path,textPath,cancellation);
                cancellation.ThrowIfCancellationRequested();
                string recognized=await File.ReadAllTextAsync(textPath,cancellation);
                result=await folder.CreateFileAsync(Path.GetFileNameWithoutExtension(file.Name)+".txt",CreationCollisionOption.GenerateUniqueName);
                progress($"Writing text for {file.Name}…");
                cancellation.ThrowIfCancellationRequested();
                await FileIO.WriteTextAsync(result,recognized).AsTask(cancellation);
                cancellation.ThrowIfCancellationRequested();
            }
            catch(OperationCanceledException) { if(result is not null) await result.DeleteAsync(); throw; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
            {
                if(result is not null) await result.DeleteAsync();
                failures.Add($"{file.Name}: {ex.Message}"); continue;
            }
            finally { if(temporary is not null) { try { temporary.Delete(true); } catch(IOException) { } catch(UnauthorizedAccessException) { } } }
            completed++;
        }
        return new(completed,files.Count,failures.AsReadOnly());
    }

    internal static async Task RunOcrWorker(string worker,string input,string textPath,Action? started,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var start=new ProcessStartInfo(worker) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=Path.GetDirectoryName(worker)! };
        start.ArgumentList.Add(input); start.ArgumentList.Add(textPath);
        using var process=Process.Start(start)??throw new InvalidOperationException("The text extraction worker could not start.");
        var error=process.StandardError.ReadToEndAsync(); var outputLog=process.StandardOutput.ReadToEndAsync();
        try { started?.Invoke(); await process.WaitForExitAsync(cancellation); }
        catch
        {
            // This handle belongs only to the worker started above. Wait for it
            // and its pipe readers before deleting its private temporary input.
            if(!process.HasExited)
            {
                try { process.Kill(true); }
                catch(InvalidOperationException) { }
                catch(System.ComponentModel.Win32Exception) when(process.HasExited) { }
            }
            await process.WaitForExitAsync(); await Task.WhenAll(error,outputLog); throw;
        }
        string failure=await error; await outputLog;
        if(process.ExitCode!=0 || !File.Exists(textPath)) throw new InvalidOperationException(string.IsNullOrWhiteSpace(failure)?"Local text extraction failed. Check the Microsoft Visual C++ x64 runtime installation.":failure.Trim());
    }

    private static string CreateEmptyFile(string path) { using var file=File.Create(path); return path; }

}
