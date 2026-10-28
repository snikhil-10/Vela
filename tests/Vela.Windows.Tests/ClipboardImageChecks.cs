using System.Runtime.InteropServices.WindowsRuntime;
using Vela.Windows.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

internal static class ClipboardImageChecks
{
    internal static async Task Run(StorageFolder root)
    {
        static void Require(bool value,string reason) { if(!value) throw new Exception(reason); }
        var folder=await root.CreateFolderAsync("atomic-png-save");
        var destination=await folder.CreateFileAsync("chosen.png"); await FileIO.WriteTextAsync(destination,"older selected contents");
        using var source=new InMemoryRandomAccessStream();
        using(var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,32,16,BitmapAlphaMode.Premultiplied))
        {
            byte[] pixels=new byte[32*16*4];
            for(int index=0;index<pixels.Length;index+=4) { pixels[index]=16; pixels[index+1]=32; pixels[index+2]=48; pixels[index+3]=128; }
            bitmap.CopyFromBuffer(pixels.AsBuffer());
            var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,source); encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
        }
        source.Seek(0); await MediaService.SavePngCopy(source,destination.Path);
        using(var encoded=await destination.OpenReadAsync())
        {
            var decoder=await BitmapDecoder.CreateAsync(encoded);
            Require(decoder.DecoderInformation.CodecId==BitmapDecoder.PngDecoderId && decoder.PixelWidth==32 && decoder.PixelHeight==16,"Saved clipboard-style bitmap is not a complete PNG.");
            using var result=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);
            byte[] pixels=new byte[32*16*4]; result.CopyToBuffer(pixels.AsBuffer());
            Require(pixels[3]==128,"PNG save lost clipboard transparency.");
        }
        byte[] before=File.ReadAllBytes(destination.Path);
        using(var invalid=new InMemoryRandomAccessStream())
        {
            bool failed=false; try { await MediaService.SavePngCopy(invalid,destination.Path); } catch(Exception error) when(error is not OperationCanceledException) { failed=true; }
            Require(failed && File.ReadAllBytes(destination.Path).SequenceEqual(before),"Failed bitmap decode damaged the selected destination.");
        }
        source.Seek(0); bool canceled=false;
        try { await MediaService.SavePngCopy(source,destination.Path,new CancellationToken(true)); } catch(OperationCanceledException) { canceled=true; }
        Require(canceled && File.ReadAllBytes(destination.Path).SequenceEqual(before),"Canceled PNG save damaged the selected destination.");
        source.Seek(0); bool busy=false;
        using(var locked=File.Open(destination.Path,FileMode.Open,FileAccess.Read,FileShare.Read))
            try { await MediaService.SavePngCopy(source,destination.Path); } catch(Exception error) when(error is IOException or UnauthorizedAccessException) { busy=true; }
        Require(busy && File.ReadAllBytes(destination.Path).SequenceEqual(before),"Busy commit changed the old PNG.");
        Require((await folder.GetFilesAsync()).Count==1,"PNG save left an unfinished staging file.");
        Console.WriteLine("INFO PNG save: complete 32x16 alpha-preserving replacement; invalid decode, cancellation and busy destination preserve prior bytes; no global clipboard or picker automation.");
    }
}
