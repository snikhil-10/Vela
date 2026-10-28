using System.Runtime.InteropServices.WindowsRuntime;
using Vela.Windows.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;

internal static class LosslessImageBudgetChecks
{
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    internal static async Task Run(StorageFolder root)
    {
        var working=await root.CreateFolderAsync("png-bmp-budgets");
        var source=await Image(working,"noise.png",512,256,true);
        var small=await Image(working,"small.png",16,8,false);
        var bad=await working.CreateFileAsync("broken.png"); await FileIO.WriteTextAsync(bad,"Not an image");
        var originals=new[]{source,small,bad}.ToDictionary(file=>file.Path,file=>File.ReadAllBytes(file.Path));
        foreach(string format in new[]{".png",".bmp"})
        {
            var outputs=await working.CreateFolderAsync(format[1..]);
            var ordinary=await MediaService.Convert([source],outputs,format,_=>{},default);
            Require(ordinary.Completed==1 && !ordinary.HasFailures,ordinary.Message);
            var prior=(await outputs.GetFilesAsync()).Single(); var priorBytes=File.ReadAllBytes(prior.Path);
            await Decode(prior,format,512,false);
            long limit=priorBytes.Length/3;
            var capped=await MediaService.Convert([source],outputs,format,_=>{},default,maximumBytes:limit);
            Require(capped.Completed==1 && !capped.HasFailures,capped.Message);
            var copy=(await outputs.GetFilesAsync()).Single(file=>file.Path!=prior.Path);
            long bytes=new FileInfo(copy.Path).Length;
            Require(bytes is >0 && bytes<=limit,"A PNG/BMP copy exceeds its actual byte cap.");
            var dimensions=await Decode(copy,format,512,true);
            Require(priorBytes.SequenceEqual(File.ReadAllBytes(prior.Path)),"A capped copy overwrote earlier output.");
            Console.WriteLine($"INFO {format[1..].ToUpperInvariant()} budget: baseline={priorBytes.Length}; limit={limit}; capped={bytes}; dimensions={dimensions.Width}x{dimensions.Height}; alpha={(format==".png"?"128":"opaque")}; prior unchanged.");
            var impossible=await MediaService.Convert([source],outputs,format,_=>{},default,maximumBytes:1);
            Require(impossible.Completed==0 && impossible.Failures.Count==1 && (await outputs.GetFilesAsync()).Count==2,"An impossible PNG/BMP cap retained output or hid failure.");
            using(var stop=new CancellationTokenSource())
            {
                bool canceled=false;
                try { await MediaService.Convert([source],outputs,format,message=> { if(message.StartsWith("Choosing")) stop.Cancel(); },stop.Token,maximumBytes:limit); }
                catch(OperationCanceledException) { canceled=true; }
                Require(canceled && (await outputs.GetFilesAsync()).Count==2,"Canceled dimension selection retained output.");
            }
            using(var stop=new CancellationTokenSource())
            {
                bool canceled=false;
                try { await MediaService.Convert([source],outputs,format,message=> { if(message.StartsWith("Writing")) stop.Cancel(); },stop.Token,maximumBytes:limit); }
                catch(OperationCanceledException) { canceled=true; }
                Require(canceled && (await outputs.GetFilesAsync()).Count==2,"Canceled final PNG/BMP write left a partial copy.");
            }
            var mixed=await working.CreateFolderAsync(format[1..]+"-mixed");
            var result=await MediaService.Convert([bad,source,small],mixed,format,_=>{},default,maximumBytes:1024);
            Require(result.Completed==1 && result.Total==3 && result.Failures.Count==2 && result.Message.Contains(bad.Name) && result.Message.Contains(source.Name),"An invalid or unfit image stopped later PNG/BMP work.");
            var tiny=(await mixed.GetFilesAsync()).Single();
            using(var stream=await tiny.OpenReadAsync())
            { var decoder=await BitmapDecoder.CreateAsync(stream); Require(decoder.PixelWidth==16 && decoder.PixelHeight==8 && stream.Size<=1024,"A fitting small image was enlarged or exceeded its cap."); }
            var partial=await working.CreateFolderAsync(format[1..]+"-partial");
            using(var stop=new CancellationTokenSource())
            {
                int started=0; bool canceled=false;
                try { await MediaService.Convert([small,source],partial,format,message=> { if(message.StartsWith("Converting") && ++started==2) stop.Cancel(); },stop.Token,maximumBytes:1024); }
                catch(OperationCanceledException) { canceled=true; }
                Require(canceled && (await partial.GetFilesAsync()).Count==1,"Canceling a later PNG/BMP image lost completed output or kept a partial copy.");
            }
        }
        var watermarks=await working.CreateFolderAsync("watermark");
        var marked=await MediaService.Convert([source],watermarks,".png",_=>{},default,quality:.9,watermark:new("Vela",.65,5),maximumBytes:100_000);
        Require(marked.Completed==1 && !marked.HasFailures,marked.Message);
        var markedFile=(await watermarks.GetFilesAsync()).Single();
        using(var input=await markedFile.OpenReadAsync())
        {
            var decoder=await BitmapDecoder.CreateAsync(input);
            var pixels=(await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Require(input.Size<=100_000 && decoder.PixelWidth<512 && pixels.Where((_,i)=>i%4==3).Any(a=>a>140),"A size-limited PNG lost its watermark or exceeded the cap.");
        }
        foreach(var file in new[]{source,small,bad})
        { Require(originals[file.Path].SequenceEqual(File.ReadAllBytes(file.Path)),"A PNG/BMP budget changed an original."); using var exclusive=File.Open(file.Path,FileMode.Open,FileAccess.Read,FileShare.None); }
        Console.WriteLine("INFO PNG/BMP budgets: impossible/mixed/small images, selection/write/next-file cancellation, watermark and original bytes/handle release pass.");
    }
    private static async Task<StorageFile> Image(StorageFolder folder,string name,int width,int height,bool noise)
    {
        var file=await folder.CreateFileAsync(name); byte[] pixels=new byte[width*height*4];
        if(noise) new Random(619).NextBytes(pixels);
        for(int i=0;i<pixels.Length;i+=4) { pixels[i]&=127; pixels[i+1]&=127; pixels[i+2]&=127; pixels[i+3]=128; }
        using var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,width,height,BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(pixels.AsBuffer());
        using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream); encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
        return file;
    }
    private static async Task<(uint Width,uint Height)> Decode(StorageFile file,string format,uint maximumEdge,bool reduced)
    {
        using var input=await file.OpenReadAsync(); var decoder=await BitmapDecoder.CreateAsync(input);
        Require(decoder.DecoderInformation.CodecId==(format==".png"?BitmapDecoder.PngDecoderId:BitmapDecoder.BmpDecoderId),"Budget changed the requested image format.");
        Require(decoder.PixelWidth<=maximumEdge && decoder.PixelWidth>=64 && Math.Abs(decoder.PixelHeight*2L-decoder.PixelWidth)<=1,"Budget enlarged an image or changed its aspect ratio.");
        Require(!reduced || decoder.PixelWidth<maximumEdge,"A restrictive PNG/BMP budget did not reduce dimensions.");
        var pixels=(await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage)).DetachPixelData();
        Require(pixels.Where((_,i)=>i%4==3).All(a=>format==".png"?a is >=127 and <=129:a==255),"PNG transparency or BMP opacity changed.");
        return (decoder.PixelWidth,decoder.PixelHeight);
    }
}
