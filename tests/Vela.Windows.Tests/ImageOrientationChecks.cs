using System.Runtime.InteropServices.WindowsRuntime;
using Vela.Windows.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;

internal static class ImageOrientationChecks
{
    // Distinct quadrants make rotation/mirroring observable independently of dimensions.
    // Labels are the visual top-left, top-right, bottom-left, bottom-right corners.
    private static readonly int[][] Corners=[ [0,1,2,3],[1,0,3,2],[3,2,1,0],[2,3,0,1],
        [0,2,1,3],[2,0,3,1],[3,1,2,0],[1,3,0,2] ];
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    internal static async Task Run(StorageFolder root)
    {
        var working=await root.CreateFolderAsync("image-orientation");
        for(ushort orientation=1;orientation<=8;orientation++)
        {
            var source=await Image(working,orientation); byte[] original=File.ReadAllBytes(source.Path);
            bool portrait=orientation>=5;
            using(var input=await source.OpenReadAsync())
            {
                var decoder=await BitmapDecoder.CreateAsync(input);
                var properties=await decoder.BitmapProperties.GetPropertiesAsync(["System.Photo.Orientation"]);
                Require(Convert.ToUInt16(properties["System.Photo.Orientation"].Value)==orientation,"Generated JPEG did not retain its EXIF tag.");
                Require(decoder.PixelWidth==320 && decoder.PixelHeight==160 && decoder.OrientedPixelWidth==(portrait?160u:320u),"Orientation fixture dimensions are wrong.");
                using var pin=await PinImageSource.Decode(input,default);
                Check(pin,orientation,320);
            }
            foreach(string format in new[]{".png",".jpg",".bmp"})
            {
                var output=await working.CreateFolderAsync($"{orientation}-{format[1..]}");
                var result=await MediaService.Convert([source],output,format,_=>{},default,128,1);
                Require(result.Completed==1 && !result.HasFailures,result.Message);
                var copy=(await output.GetFilesAsync()).Single();
                await Check(copy,orientation,128);
                if(format==".jpg") continue;
                var before=File.ReadAllBytes(copy.Path);
                var capped=await MediaService.Convert([source],output,format,_=>{},default,128,maximumBytes:10000);
                Require(capped.Completed==1 && !capped.HasFailures,capped.Message);
                var cappedCopy=(await output.GetFilesAsync()).Single(file=>file.Path!=copy.Path);
                Require(new FileInfo(cappedCopy.Path).Length is >0 and <=10000,"Oriented output exceeded its byte cap.");
                await Check(cappedCopy,orientation,128);
                Require(before.SequenceEqual(File.ReadAllBytes(copy.Path)),"Orientation budget overwrote prior output.");
            }
            // Inspect the real native preparation path without making a recognition-quality claim.
            var texts=await working.CreateFolderAsync($"ocr-{orientation}"); bool prepared=false;
            var ocr=await MediaService.Ocr([source],texts,_=>{},default,async (input,text,token)=>
            {
                await Check(await StorageFile.GetFileFromPathAsync(input),orientation,320);
                prepared=true; await File.WriteAllTextAsync(text,"Prepared orientation "+orientation,token);
            });
            Require(prepared && ocr.Completed==1 && !ocr.HasFailures,"OCR did not normalize orientation before invoking recognition.");
            Require(original.SequenceEqual(File.ReadAllBytes(source.Path)),"An oriented original was modified.");
            using(var exclusive=File.Open(source.Path,FileMode.Open,FileAccess.Read,FileShare.None)) { }
            Console.WriteLine($"INFO EXIF orientation {orientation}: three formats, capped PNG/BMP, pin decode and native OCR preparation preserve corner order/aspect; original unchanged.");
        }
    }
    private static async Task<StorageFile> Image(StorageFolder folder,ushort orientation)
    {
        byte[] pixels=new byte[320*160*4]; var random=new Random(145);
        for(int y=0;y<160;y++) for(int x=0;x<320;x++)
        {
            int corner=(y<80?0:2)+(x<160?0:1),p=(y*320+x)*4;
            pixels[p]=(byte)(corner==2?220+random.Next(35):random.Next(35));
            pixels[p+1]=(byte)(corner is 1 or 3?220+random.Next(35):random.Next(35));
            pixels[p+2]=(byte)(corner is 0 or 3?220+random.Next(35):random.Next(35));
            pixels[p+3]=255;
        }
        var file=await folder.CreateFileAsync($"orientation-{orientation}.jpg");
        using var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,320,160,BitmapAlphaMode.Ignore);
        bitmap.CopyFromBuffer(pixels.AsBuffer());
        using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId,stream,new BitmapPropertySet{["ImageQuality"]=new(1f,PropertyType.Single)});
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet{["System.Photo.Orientation"]=new(orientation,PropertyType.UInt16)});
        await encoder.FlushAsync(); return file;
    }
    private static async Task Check(StorageFile file,ushort orientation,uint maximumEdge)
    {
        using var stream=await file.OpenReadAsync(); var decoder=await BitmapDecoder.CreateAsync(stream);
        // Output pixels must already be normalized; ignoring EXIF must still display them correctly.
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage);
        Check(bitmap,orientation,maximumEdge);
    }
    private static void Check(SoftwareBitmap bitmap,ushort orientation,uint maximumEdge)
    {
        int width=bitmap.PixelWidth,height=bitmap.PixelHeight;
        Require(Math.Max(width,height)<=maximumEdge && Math.Min(width,height)>=32,"Orientation resize exceeded its edge bound.");
        Require(orientation>=5?Math.Abs(height-2*width)<=1:Math.Abs(width-2*height)<=1,"EXIF changed aspect ratio or failed to swap dimensions.");
        byte[] pixels=new byte[width*height*4]; bitmap.CopyToBuffer(pixels.AsBuffer());
        for(int corner=0;corner<4;corner++)
        {
            int x=corner%2==0?width/4:width*3/4,y=corner<2?height/4:height*3/4,p=(y*width+x)*4;
            int actual=pixels[p]>150?2:pixels[p+2]>150?(pixels[p+1]>150?3:0):1;
            Require(actual==Corners[orientation-1][corner],$"EXIF {orientation}, corner {corner}: expected {Corners[orientation-1][corner]}, got {actual}.");
        }
    }
}
