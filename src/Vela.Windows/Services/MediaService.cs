using Windows.Graphics.Imaging;
using Windows.Foundation;
using System.Diagnostics;
using Vela.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace Vela.Windows.Services;

internal static class MediaService
{
    public static async Task<string> SaveClipboardImage(nint hwnd)
    {
        var content=Clipboard.GetContent();
        if(!content.Contains(StandardDataFormats.Bitmap)) return "Copy an image first, then try again.";
        var picker=new FileSavePicker { SuggestedFileName="Clipboard image" };
        picker.FileTypeChoices.Add("PNG image",[".png"]); InitializeWithWindow.Initialize(picker,hwnd);
        var file=await picker.PickSaveFileAsync(); if(file is null) return "Save canceled.";
        using var source=await (await content.GetBitmapAsync()).OpenReadAsync();
        var decoder=await BitmapDecoder.CreateAsync(source);
        using var bitmap=await decoder.GetSoftwareBitmapAsync();
        using var output=await file.OpenAsync(FileAccessMode.ReadWrite); output.Size=0;
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output);
        encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
        return $"Saved {file.Name}.";
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
    public static async Task<int> Convert(IReadOnlyList<StorageFile> files,StorageFolder folder,string extension,Action<string> progress,CancellationToken cancellation,uint longestEdge=0,double quality=.9,ImageWatermark? watermark=null)
    {
        int completed=0;
        Guid format=extension switch { ".jpg"=>BitmapEncoder.JpegEncoderId,".bmp"=>BitmapEncoder.BmpEncoderId,_=>BitmapEncoder.PngEncoderId };
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested(); progress($"Converting {file.Name}…");
            using var source=await file.OpenReadAsync(); var decoder=await BitmapDecoder.CreateAsync(source);
            if((long)decoder.PixelWidth*decoder.PixelHeight>100_000_000) throw new InvalidOperationException($"{file.Name} exceeds the 100-megapixel processing limit.");
            var size=ImageSizing.Fit(decoder.PixelWidth,decoder.PixelHeight,longestEdge);
            using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,extension==".jpg"?BitmapAlphaMode.Ignore:BitmapAlphaMode.Premultiplied,
                new BitmapTransform { ScaledWidth=size.Width,ScaledHeight=size.Height,InterpolationMode=BitmapInterpolationMode.Fant },ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb);
            if(watermark is not null) await Task.Run(()=>ImageWatermarks.Apply(bitmap,watermark,cancellation),cancellation);
            var result=await folder.CreateFileAsync(Path.GetFileNameWithoutExtension(file.Name)+extension,CreationCollisionOption.GenerateUniqueName);
            try
            {
                using var output=await result.OpenAsync(FileAccessMode.ReadWrite);
                var encoder=await BitmapEncoder.CreateAsync(format,output); encoder.SetSoftwareBitmap(bitmap);
                if(extension==".jpg") await encoder.BitmapProperties.SetPropertiesAsync(new Dictionary<string,BitmapTypedValue> { ["ImageQuality"]=new((float)Math.Clamp(quality,.1,1),PropertyType.Single) });
                cancellation.ThrowIfCancellationRequested(); await encoder.FlushAsync(); completed++;
            }
            catch { await result.DeleteAsync(); throw; }
        }
        return completed;
    }
    public static async Task<int> Ocr(IReadOnlyList<StorageFile> files,StorageFolder folder,Action<string> progress,CancellationToken cancellation)
    {
        string worker=Path.Combine(AppContext.BaseDirectory,"Ocr","Vela.OcrWorker.exe");
        if(!File.Exists(worker)) throw new InvalidOperationException("The OCR worker is missing. Extract the complete Vela download before using text extraction.");
        int completed=0;
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested(); progress($"Reading text in {file.Name}…");
            string temporary=Path.Combine(Path.GetTempPath(),"VelaOCR-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
            try
            {
                using var source=await file.OpenReadAsync(); var decoder=await BitmapDecoder.CreateAsync(source);
                if((long)decoder.PixelWidth*decoder.PixelHeight>100_000_000) throw new InvalidOperationException($"{file.Name} exceeds the 100-megapixel processing limit.");
                var size=ImageSizing.Fit(decoder.PixelWidth,decoder.PixelHeight,4096);
                using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,
                    new BitmapTransform { ScaledWidth=size.Width,ScaledHeight=size.Height },ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb);
                var input=await StorageFile.GetFileFromPathAsync(CreateEmptyFile(Path.Combine(temporary,"input.png")));
                using(var stream=await input.OpenAsync(FileAccessMode.ReadWrite))
                { var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream); encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync(); }
                string textPath=Path.Combine(temporary,"text.txt");
                var start=new ProcessStartInfo(worker) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=Path.GetDirectoryName(worker)! };
                start.ArgumentList.Add(input.Path); start.ArgumentList.Add(textPath);
                using var process=Process.Start(start)??throw new InvalidOperationException("The text extraction worker could not start.");
                var error=process.StandardError.ReadToEndAsync(); var outputLog=process.StandardOutput.ReadToEndAsync();
                try { await process.WaitForExitAsync(cancellation); }
                catch(OperationCanceledException) { try { process.Kill(true); await process.WaitForExitAsync(); } catch(InvalidOperationException) { } throw; }
                string failure=await error; await outputLog;
                if(process.ExitCode!=0 || !File.Exists(textPath)) throw new InvalidOperationException(string.IsNullOrWhiteSpace(failure)?"Local text extraction failed. Check the Microsoft Visual C++ x64 runtime installation.":failure.Trim());
                cancellation.ThrowIfCancellationRequested();
                var output=await folder.CreateFileAsync(Path.GetFileNameWithoutExtension(file.Name)+".txt",CreationCollisionOption.GenerateUniqueName);
                try { await FileIO.WriteTextAsync(output,await File.ReadAllTextAsync(textPath,cancellation)); completed++; }
                catch { await output.DeleteAsync(); throw; }
            }
            finally { try { Directory.Delete(temporary,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
        }
        return completed;
    }

    private static string CreateEmptyFile(string path) { using var file=File.Create(path); return path; }

}
