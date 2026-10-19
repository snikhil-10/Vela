using System.Runtime.InteropServices.WindowsRuntime;
using Vela.Windows.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;

internal static class ImageBatchChecks
{
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    internal static async Task Run(StorageFolder root)
    {
        var working=await root.CreateFolderAsync("image-batch-failures");
        var one=await Image(await working.CreateFolderAsync("one"),32);
        var two=await Image(await working.CreateFolderAsync("two"),40);
        var bad=await working.CreateFileAsync("broken.png"); await FileIO.WriteTextAsync(bad,"Not an image");
        var empty=await working.CreateFileAsync("empty.jpg");
        var originals=new[]{one,two,bad,empty}.ToDictionary(file=>file.Path,file=>File.ReadAllBytes(file.Path));
        var outputs=await working.CreateFolderAsync("outputs");
        var first=await MediaService.Convert([one],outputs,".png",_=>{},default,200);
        Require(first.Completed==1 && !first.HasFailures,first.Message);
        var prior=(await outputs.GetFilesAsync()).Single(); byte[] priorBytes=File.ReadAllBytes(prior.Path);
        var result=await MediaService.Convert([bad,one,empty,two],outputs,".png",_=>{},default,200);
        Require(result.Completed==2 && result.Total==4 && result.HasFailures && result.Failures.Count==2 && result.Message.Contains("broken.png") && result.Message.Contains("empty.jpg"),"Image batch hid a failure or stopped before valid later files.");
        Require((await outputs.GetFilesAsync()).Count==3 && priorBytes.SequenceEqual(File.ReadAllBytes(prior.Path)),"Image batch replaced an earlier output or left an extra partial copy.");
        foreach(var copy in await outputs.GetFilesAsync()) await Decode(copy);
        Console.WriteLine($"INFO Image mixed batch: {result.Completed}/{result.Total} completed; {result.Failures.Count} named failures; three unique decodable PNGs including prior output.");
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false;
            try { await MediaService.Convert([one],outputs,".png",message=> { if(message.StartsWith("Writing")) stop.Cancel(); },stop.Token,200); }
            catch(OperationCanceledException) { canceled=true; }
            Require(canceled && (await outputs.GetFilesAsync()).Count==3,"Cancellation after creating an image output left a partial copy.");
        }
        var before=(await outputs.GetFilesAsync()).Select(file=>file.Path).ToHashSet();
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false; int started=0;
            try
            {
                await MediaService.Convert([one,two],outputs,".png",message=>
                { if(message.StartsWith("Converting") && ++started==2) stop.Cancel(); },stop.Token,200);
            }
            catch(OperationCanceledException) { canceled=true; }
            var retained=(await outputs.GetFilesAsync()).Where(file=>!before.Contains(file.Path)).ToArray();
            Require(canceled && started==2 && retained.Length==1,"Canceling the next image lost an earlier completed copy or retained partial output.");
            await Decode(retained[0]);
        }
        bool preCanceled=false;
        try { await MediaService.Convert([one],outputs,".png",_=>{},new CancellationToken(true)); }
        catch(OperationCanceledException) { preCanceled=true; }
        Require(preCanceled && (await outputs.GetFilesAsync()).Count==4,"Pre-canceled image conversion created a copy.");
        bool rejected=false;
        try { await MediaService.Convert([one],outputs,".gif",_=>{},default); }
        catch(ArgumentOutOfRangeException) { rejected=true; }
        Require(rejected && (await outputs.GetFilesAsync()).Count==4,"Unsupported output silently wrote a mislabeled PNG.");
        foreach(var source in new[]{one,two,bad,empty})
        {
            Require(originals[source.Path].SequenceEqual(File.ReadAllBytes(source.Path)),"Image batch changed an original.");
            using var exclusive=File.Open(source.Path,FileMode.Open,FileAccess.Read,FileShare.None);
        }
        Console.WriteLine("INFO Image cancellation: partial new output removed; one earlier completed output retained; source handles released and bytes unchanged.");
    }
    private static async Task<StorageFile> Image(StorageFolder folder,byte blue)
    {
        var file=await folder.CreateFileAsync("same.png"); byte[] pixels=new byte[320*160*4];
        for(int i=0;i<pixels.Length;i+=4) { pixels[i]=blue; pixels[i+1]=64; pixels[i+2]=96; pixels[i+3]=128; }
        using(var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,320,160,BitmapAlphaMode.Premultiplied))
        using(var output=await file.OpenAsync(FileAccessMode.ReadWrite))
        { bitmap.CopyFromBuffer(pixels.AsBuffer()); var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output); encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync(); }
        return file;
    }
    private static async Task Decode(StorageFile file)
    {
        using var input=await file.OpenReadAsync(); var decoder=await BitmapDecoder.CreateAsync(input);
        Require(decoder.PixelWidth==200 && decoder.PixelHeight==100,"Mixed image batch changed resize dimensions.");
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);
        byte[] pixels=new byte[200*100*4]; bitmap.CopyToBuffer(pixels.AsBuffer());
        Require(pixels[3] is >=127 and <=129,"Mixed image batch lost PNG alpha.");
    }
}
