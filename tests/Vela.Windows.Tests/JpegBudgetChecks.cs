using System.Runtime.InteropServices.WindowsRuntime;
using Vela.Windows.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;

internal static class JpegBudgetChecks
{
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    internal static async Task Run(StorageFolder root)
    {
        var source=await root.CreateFileAsync("jpeg-noise.bmp");
        byte[] pixels=new byte[512*384*4]; new Random(1729).NextBytes(pixels);
        for(int i=3;i<pixels.Length;i+=4) pixels[i]=255;
        using(var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,512,384,BitmapAlphaMode.Ignore))
        using(var stream=await source.OpenAsync(FileAccessMode.ReadWrite))
        {
            bitmap.CopyFromBuffer(pixels.AsBuffer());
            var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId,stream);
            encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
        }
        byte[] original=File.ReadAllBytes(source.Path);
        var outputs=await root.CreateFolderAsync("jpeg-budgets");
        var ordinary=await MediaService.Convert([source],outputs,".jpg",_=>{},default,quality:1);
        Require(ordinary.Completed==1 && !ordinary.HasFailures,"Default conversion failed.");
        var first=(await outputs.GetFilesAsync()).Single();
        var lowFolder=await root.CreateFolderAsync("jpeg-quality-probe");
        await MediaService.Convert([source],lowFolder,".jpg",_=>{},default,quality:.1);
        long highBytes=new FileInfo(first.Path).Length;
        long lowBytes=new FileInfo((await lowFolder.GetFilesAsync()).Single().Path).Length;
        Console.WriteLine($"INFO JPEG quality probe: quality100={highBytes} bytes; quality10={lowBytes} bytes.");
        Require(lowBytes<highBytes,"JPEG quality did not change compression for a noise fixture.");
        long limit=new FileInfo(first.Path).Length/2;
        Require(limit>1024,"Noise fixture is too small to exercise the budget.");
        var budgeted=await MediaService.Convert([source],outputs,".jpg",_=>{},default,quality:1,maximumBytes:limit);
        Require(budgeted.Completed==1 && !budgeted.HasFailures,"Budget conversion failed.");
        var capped=(await outputs.GetFilesAsync()).Single(file=>file.Path!=first.Path);
        Require(new FileInfo(capped.Path).Length is >0 && new FileInfo(capped.Path).Length<=limit,"Output exceeds its actual byte cap.");
        using(var stream=await capped.OpenReadAsync())
        {
            var decoder=await BitmapDecoder.CreateAsync(stream);
            Require(decoder.PixelWidth==512 && decoder.PixelHeight==384,"Budget changed dimensions rather than quality.");
        }
        var impossible=await MediaService.Convert([source],outputs,".jpg",_=>{},default,maximumBytes:1);
        Require(impossible.Completed==0 && impossible.HasFailures && (await outputs.GetFilesAsync()).Count==2,"An impossible budget left an output.");
        bool rejected=false;
        try { await MediaService.Convert([source],outputs,".png",_=>{},default,maximumBytes:limit); }
        catch(InvalidOperationException) { rejected=true; }
        Require(rejected && (await outputs.GetFilesAsync()).Count==2,"A PNG budget was silently accepted.");
        var small=await root.CreateFileAsync("small.png");
        using(var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,16,16,BitmapAlphaMode.Ignore))
        using(var stream=await small.OpenAsync(FileAccessMode.ReadWrite))
        { var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream); encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync(); }
        var mixedOutputs=await root.CreateFolderAsync("jpeg-mixed-budgets");
        var mixed=await MediaService.Convert([source,small],mixedOutputs,".jpg",_=>{},default,maximumBytes:4096);
        Require(mixed.Completed==1 && mixed.Total==2 && mixed.Failures.Count==1 && mixed.Message.Contains(source.Name),"An unfit JPEG stopped a later image or hid its failure.");
        var mixedCopy=(await mixedOutputs.GetFilesAsync()).Single();
        Require(new FileInfo(mixedCopy.Path).Length is >0 and <=4096,"Mixed JPEG batch kept an oversized copy.");
        Console.WriteLine($"INFO JPEG mixed budget: 1/2 completed; one named size failure; retained={new FileInfo(mixedCopy.Path).Length} bytes under 4096.");
        using(var canceled=new CancellationTokenSource())
        {
            bool stopped=false;
            try
            {
                await MediaService.Convert([source],outputs,".jpg",message=>
                { if(message.StartsWith("Choosing JPEG")) canceled.Cancel(); },canceled.Token,maximumBytes:limit);
            }
            catch(OperationCanceledException) { stopped=true; }
            Require(stopped && (await outputs.GetFilesAsync()).Count==2,"Canceled quality selection left an output.");
        }
        Require(original.SequenceEqual(File.ReadAllBytes(source.Path)),"JPEG conversion changed the original.");
    }
}
