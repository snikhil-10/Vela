using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using System.Runtime.InteropServices.WindowsRuntime;
using Vela.Windows.Services;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if(args.Length==1 && args[0]=="--microphone-roundtrip") return MicrophoneChecks.RoundTrip();
        if(args.Length==1 && args[0]=="--clipboard-metadata") return TextClipboardChecks.Metadata();
        if(args.Length==1 && args[0]=="--clipboard-publication-check") return TextClipboardChecks.Run();
        if(args.Length==1 && args[0]=="--clipboard-publication-restored-check") return TextClipboardChecks.CheckRestored();
        if(args.Length==1 && args[0] is "--clipboard-seed" or "--clipboard-check") return ClipboardFixture(args[0]);
        return Run().GetAwaiter().GetResult();
    }
    private static int ClipboardFixture(string mode)
    {
        const string fixture="Vela plain paste test";
        if(mode=="--clipboard-seed")
        {
            var data=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetText(fixture);
            data.SetHtmlFormat(global::Windows.ApplicationModel.DataTransfer.HtmlFormatHelper.CreateHtmlFormat("<b>"+fixture+"</b>"));
            if(!global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions(data,new global::Windows.ApplicationModel.DataTransfer.ClipboardContentOptions { IsAllowedInHistory=false,IsRoamable=false }))
            { Console.WriteLine("FAIL Windows declined the clipboard fixture write."); return 1; }
            global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush(); Console.WriteLine("Seeded test text and HTML clipboard formats."); return 0;
        }
        var view=global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        bool valid=view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Html) && view.GetTextAsync().AsTask().GetAwaiter().GetResult()==fixture && view.GetHtmlFormatAsync().AsTask().GetAwaiter().GetResult().Contains("<b>"+fixture+"</b>");
        Console.WriteLine(valid?"PASS Original text and HTML clipboard formats are preserved.":"FAIL Original clipboard formats changed."); return valid?0:1;
    }
    private static async Task<int> Run()
    {
int passed=0,failed=0;
async Task Check(string title,Func<Task> action)
{ try { await action(); passed++; Console.WriteLine("PASS "+title); } catch(Exception ex) { failed++; Console.WriteLine($"FAIL {title}: {ex.GetType().Name}: {ex.Message}"); } }
void Require(bool condition,string reason) { if(!condition) throw new InvalidOperationException(reason); }
string root=Path.Combine(Path.GetTempPath(),"Vela-Windows-Checks-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
try
{
    var folder=await StorageFolder.GetFolderFromPathAsync(root);
    var fixture=await StorageFile.GetFileFromPathAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","ocr-english.png"));
    var images=await folder.CreateFolderAsync("images");
    await Check("Validation profiles isolate diagnostics, reject invalid paths and cannot change sign-in startup",()=>
    { AppDataPathsChecks.Run(root); return Task.CompletedTask; });
    await Check("Physical microphone enumeration reads mute state without recording or writes",()=>
    {
        var microphones=MicrophoneService.List();
        Require(microphones.All(device=>!string.IsNullOrWhiteSpace(device.Name)),"An endpoint returned an empty name.");
        Console.WriteLine($"INFO Read-only Core Audio probe: {microphones.Count} active capture endpoints. Mute writes and hot-plug were not tested.");
        return Task.CompletedTask;
    });
    await Check("Shelf cancels delayed providers on disable or close and accepts a fresh session",ShelfChecks.Cancellation);
    await Check("Shelf late drag completion preserves identical new entries and accepts only Copy",ShelfChecks.DragIdentity);
    await Check("JPEG byte budgets produce decodable capped copies and cleanly reject impossible or canceled work",()=>JpegBudgetChecks.Run(folder));
    await Check("Image batches isolate unreadable inputs, retain unique alpha-preserving copies and clean up canceled work",()=>ImageBatchChecks.Run(folder));
    await Check("Clipboard snapshots preserve formats and transfer intent and reject failed providers before editing",async ()=>
    {
        var data=new global::Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation=global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move }; data.SetText("Mixed CASE ✓\r\nSecond line");
        string html=global::Windows.ApplicationModel.DataTransfer.HtmlFormatHelper.CreateHtmlFormat("<b>Mixed CASE</b>"); data.SetHtmlFormat(html);
        data.SetBitmap(global::Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(fixture));
        using(var snapshot=await ClipboardSnapshot.Capture(data.GetView(),CancellationToken.None))
        {
            Require(snapshot.Text=="Mixed CASE ✓\r\nSecond line","Unicode or newlines changed in the paste payload.");
            Require(snapshot.Package.RequestedOperation==global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move,"Original clipboard transfer intent changed.");
            Require(await snapshot.Package.GetView().GetHtmlFormatAsync()==html,"Rich clipboard data was discarded.");
            using var bitmap=await (await snapshot.Package.GetView().GetBitmapAsync()).OpenReadAsync();
            Require(bitmap.Size==(ulong)new FileInfo(fixture.Path).Length,"Bitmap clipboard data was truncated.");
        }
        var failedProvider=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); failedProvider.SetText("untouched");
        failedProvider.SetDataProvider("vela.qa.failed",_=>throw new InvalidOperationException("Unavailable fixture provider"));
        bool rejected=false;
        try { using var snapshot=await ClipboardSnapshot.Capture(failedProvider.GetView(),CancellationToken.None); } catch(Exception) { rejected=true; }
        Require(rejected && await failedProvider.GetView().GetTextAsync()=="untouched","Provider failure changed the original package or was accepted.");
        var excessive=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); excessive.SetText("untouched");
        for(int i=0;i<64;i++) excessive.SetData("vela.qa.format."+i,"fixture");
        rejected=false;
        try { using var snapshot=await ClipboardSnapshot.Capture(excessive.GetView(),CancellationToken.None); }
        catch(InvalidOperationException) { rejected=true; }
        Require(rejected && await excessive.GetView().GetTextAsync()=="untouched","Excessive clipboard formats were accepted or mutated.");
        using var canceled=new CancellationTokenSource(); canceled.Cancel();
        bool stopped=false;
        try { using var snapshot=await ClipboardSnapshot.Capture(data.GetView(),canceled.Token); } catch(OperationCanceledException) { stopped=true; }
        Require(stopped,"Canceled preparation continued.");
    });
    await Check("Shelf transfers every selected file, preserves originals and retains canceled drags",async ()=>
    {
        var one=await folder.CreateFileAsync("shelf-one.txt"); await FileIO.WriteTextAsync(one,"first original");
        var two=await folder.CreateFileAsync("shelf-two.txt"); await FileIO.WriteTextAsync(two,"second original");
        var data=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetStorageItems([one,two]);
        var shelf=new ShelfService(); Require(await shelf.Add(data.GetView())==2,"Dropped file selection was truncated.");
        var dragged=shelf.Items.ToArray(); var package=ShelfService.Package(dragged);
        Require((await package.GetView().GetStorageItemsAsync()).Count==2,"Drag-out lost selected files.");
        shelf.CompleteDrag(dragged,global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.None,true);
        Require(shelf.Items.Count==2,"Canceled drag removed shelf entries.");
        shelf.CompleteDrag(dragged,global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy,false);
        Require(shelf.Items.Count==2,"Disabled remove-after-drop was ignored.");
        shelf.CompleteDrag(dragged,global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy,true);
        Require(shelf.Items.Count==0 && await FileIO.ReadTextAsync(one)=="first original" && await FileIO.ReadTextAsync(two)=="second original","Transfer modified originals.");
        await shelf.Add(data.GetView()); await one.DeleteAsync();
        try { ShelfService.Package(shelf.Items.ToArray()); throw new Exception("Missing original was not rejected."); }
        catch(InvalidOperationException) { }
    });
    await Check("Shelf retains Unicode text and links and rejects oversized additions atomically",async ()=>
    {
        var shelf=new ShelfService(); var text=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); text.SetText("こんにちは\nsecond line");
        await shelf.Add(text.GetView());
        Require(await ShelfService.Package(shelf.Items.ToArray()).GetView().GetTextAsync()=="こんにちは\nsecond line","Shelf text was altered.");
        var link=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); link.SetWebLink(new Uri("https://example.com/a?keep=1")); await shelf.Add(link.GetView());
        Require(shelf.Items[1].Text=="https://example.com/a?keep=1","Web link was not retained.");
        var huge=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); huge.SetText(new string('x',262145));
        try { await shelf.Add(huge.GetView()); throw new Exception("Oversized shelf text accepted."); } catch(InvalidOperationException) { }
        Require(shelf.Items.Count==2,"Rejected addition changed existing shelf entries.");
        for(int i=shelf.Items.Count;i<ShelfService.Capacity;i++) shelf.Items.Add(new("filler","filler",null));
        try { await shelf.Add(text.GetView()); throw new Exception("Full shelf accepted another entry."); } catch(InvalidOperationException) { }
        Require(shelf.Items.Count==ShelfService.Capacity,"Shelf capacity is not bounded.");
    });

    await Check("Image conversion resizes, preserves originals and uses unique names",async ()=>
    {
        var count=await MediaService.Convert([fixture],images,".png",_=>{},CancellationToken.None,500);
        Require(count.Completed==1 && !count.HasFailures,"One conversion expected.");
        var again=await MediaService.Convert([fixture],images,".png",_=>{},CancellationToken.None,500);
        var outputs=await images.GetFilesAsync(); Require(again.Completed==1 && !again.HasFailures && outputs.Count==2,"Name collision overwrote output.");
        using var input=await outputs[0].OpenReadAsync(); var decoded=await BitmapDecoder.CreateAsync(input);
        Require(decoded.PixelWidth==500 && decoded.PixelHeight==80,"Resize did not preserve aspect ratio.");
        Require(File.Exists(fixture.Path),"Original was removed.");
    });
    await Check("JPEG quality conversion is decodable",async ()=>
    {
        await MediaService.Convert([fixture],images,".jpg",_=>{},CancellationToken.None,0,.75);
        var file=(await images.GetFilesAsync()).First(f=>f.FileType==".jpg");
        using var input=await file.OpenReadAsync(); var decoded=await BitmapDecoder.CreateAsync(input);
        Require(decoded.PixelWidth==1000 && decoded.PixelHeight==160,"JPEG dimensions changed.");
    });
    await Check("Image pin previews are bounded, preserve alpha and honor cancellation",async ()=>
    {
        var sourceFile=await folder.CreateFileAsync("pin-preview.png");
        byte[] pixels=new byte[4096*1024*4];
        for(int i=0;i<pixels.Length;i+=4) { pixels[i]=32; pixels[i+1]=64; pixels[i+2]=96; pixels[i+3]=128; }
        using(var source=new SoftwareBitmap(BitmapPixelFormat.Bgra8,4096,1024,BitmapAlphaMode.Premultiplied))
        using(var stream=await sourceFile.OpenAsync(FileAccessMode.ReadWrite))
        { source.CopyFromBuffer(pixels.AsBuffer()); var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream); encoder.SetSoftwareBitmap(source); await encoder.FlushAsync(); }
        byte[] original=File.ReadAllBytes(sourceFile.Path);
        using(var stream=await sourceFile.OpenReadAsync())
        using(var preview=await PinImageSource.Decode(stream,CancellationToken.None))
        {
            Require(preview.PixelWidth==2048 && preview.PixelHeight==512,"Preview does not respect its memory bound or aspect ratio.");
            Require(preview.BitmapPixelFormat==BitmapPixelFormat.Bgra8 && preview.BitmapAlphaMode==BitmapAlphaMode.Premultiplied,"Pin image format is unsuitable for native display.");
            byte[] result=new byte[preview.PixelWidth*preview.PixelHeight*4]; preview.CopyToBuffer(result.AsBuffer());
            Require(result[3] is >=127 and <=129 && result[0]<=result[3] && result[2]<=result[3],"Preview alpha was lost.");
        }
        Require(original.SequenceEqual(File.ReadAllBytes(sourceFile.Path)),"Preview changed its source.");
        using(var stream=await sourceFile.OpenReadAsync())
        {
            try { using var preview=await PinImageSource.Decode(stream,new CancellationToken(true)); throw new InvalidOperationException("Canceled preview was created."); }
            catch(OperationCanceledException) { }
        }
        using(var invalid=new global::Windows.Storage.Streams.InMemoryRandomAccessStream())
        {
            bool rejected=false;
            try { using var preview=await PinImageSource.Decode(invalid,CancellationToken.None); }
            catch(Exception ex) when(ex is not OperationCanceledException) { rejected=true; }
            Require(rejected,"Malformed image was accepted.");
        }
    });
    await Check("Text watermarks respect placement, resize and PNG transparency",async ()=>
    {
        var watermarks=await folder.CreateFolderAsync("watermarks");
        var sourceFile=await folder.CreateFileAsync("transparent.png");
        using(var blank=new SoftwareBitmap(BitmapPixelFormat.Bgra8,512,256,BitmapAlphaMode.Premultiplied))
        using(var stream=await sourceFile.OpenAsync(FileAccessMode.ReadWrite))
        { blank.CopyFromBuffer(new byte[512*256*4].AsBuffer()); var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream); encoder.SetSoftwareBitmap(blank); await encoder.FlushAsync(); }
        foreach(string position in new[]{"Bottom right","Bottom left","Top right","Top left"})
        {
            var existing=(await watermarks.GetFilesAsync()).Select(f=>f.Path).ToHashSet();
            var watermarked=await MediaService.Convert([sourceFile],watermarks,".png",_=>{},CancellationToken.None,256,.9,new("Vela",.65,3,position));
            Require(watermarked.Completed==1 && !watermarked.HasFailures,"Watermark conversion failed.");
            var output=(await watermarks.GetFilesAsync()).Single(f=>!existing.Contains(f.Path));
            using var stream=await output.OpenReadAsync(); var decoder=await BitmapDecoder.CreateAsync(stream);
            Require(decoder.PixelWidth==256 && decoder.PixelHeight==128,"Watermarked output resize differs.");
            var pixels=(await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage)).DetachPixelData();
            int marked=0;
            for(int y=0;y<128;y++) for(int x=0;x<256;x++)
            {
                int p=(y*256+x)*4; if(pixels[p+3]==0) continue; marked++;
                Require(pixels[p+3]<=167,"Opacity was not preserved.");
                Require(pixels[p]<=pixels[p+3] && pixels[p+1]<=pixels[p+3] && pixels[p+2]<=pixels[p+3],"Premultiplied alpha was corrupted.");
                Require(position.StartsWith("Top")?y<64:y>=64,"Vertical corner placement differs.");
                Require(position.EndsWith("left")?x<128:x>=128,"Horizontal corner placement differs.");
            }
            Require(marked>10 && marked<1000,"Watermark is empty or damaged the transparent background.");
        }
        using(var original=await sourceFile.OpenReadAsync())
        { var decoder=await BitmapDecoder.CreateAsync(original); Require(decoder.PixelWidth==512 && decoder.PixelHeight==256,"Watermark changed the original."); }
        int before=(await watermarks.GetFilesAsync()).Count;
        try { await MediaService.Convert([sourceFile],watermarks,".png",_=>{},new CancellationToken(true),256,.9,new("Canceled")); throw new InvalidOperationException("Canceled conversion continued."); }
        catch(OperationCanceledException) { }
        Require((await watermarks.GetFilesAsync()).Count==before,"Canceled conversion left a file.");
    });
    await Check("OCR worker extracts English text offline",async ()=>
    {
        var text=await folder.CreateFolderAsync("text");
        Require(await MediaService.Ocr([fixture],text,_=>{},CancellationToken.None)==1,"OCR returned no output.");
        string result=await FileIO.ReadTextAsync((await text.GetFilesAsync()).Single());
        Require(result.Contains("Vela local text extraction",StringComparison.OrdinalIgnoreCase),"Recognized text differs from fixture.");
    });
    await Check("Review scan keeps recent copies and scans only the selected tree",()=>
    {
        string review=Path.Combine(root,"review"); Directory.CreateDirectory(review);
        string stale=Path.Combine(review,"stale.txt"),recent=Path.Combine(review,"recent.txt"),copy=Path.Combine(review,"recent-copy.txt");
        File.WriteAllText(stale,"fixture"); File.WriteAllText(recent,"fixture"); File.WriteAllText(copy,"fixture");
        File.SetCreationTimeUtc(stale,DateTime.UtcNow.AddDays(-100)); File.SetLastWriteTimeUtc(stale,DateTime.UtcNow.AddDays(-100)); File.SetLastWriteTimeUtc(copy,DateTime.UtcNow.AddDays(-100));
        var result=DownloadReview.Scan(review,30,CancellationToken.None);
        Require(result.Files.Count==1 && result.Files[0].Path==stale,"Recent or unrelated file was included."); return Task.CompletedTask;
    });
    await Check("Download review rejects changed and out-of-root selections without recycling",()=>DownloadSafetyChecks.Run(root));
    await Check("Missing GPU and DDC capabilities return cleanly without writes",async ()=>
    {
        using var sensors=new GpuSensors(); var readings=sensors.Read();
        var monitors=await MonitorBrightness.Read();
        Console.WriteLine($"INFO Read-only capability probe: {readings.Count} GPU sensors, {monitors.Count} DDC displays. Hardware writes were not tested.");
    });
    await Check("Batch video conversion and audio extraction decode through Windows codecs",async ()=>
    {
        var videoFolder=await folder.CreateFolderAsync("video");
        var video=await videoFolder.CreateFileAsync("source.mp4");
        string wave=Path.Combine(videoFolder.Path,"tone.wav");
        using(var writer=new BinaryWriter(File.Create(wave)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+96000); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(96000);
            for(int i=0;i<48000;i++) writer.Write((short)(Math.Sin(i*2*Math.PI*440/48000)*4000));
        }
        var clip=await MediaClip.CreateFromImageFileAsync(fixture,TimeSpan.FromSeconds(1)); var composition=new MediaComposition(); composition.Clips.Add(clip);
        composition.BackgroundAudioTracks.Add(await BackgroundAudioTrack.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(wave)));
        var render=await composition.RenderToFileAsync(video,MediaTrimmingPreference.Precise,MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p));
        Require(render==TranscodeFailureReason.None,"Fixture video encoder failed: "+render);
        var converted=await videoFolder.CreateFolderAsync("converted");
        var result=await VideoService.Convert([video],converted,VideoEncodingQuality.HD720p,false,_=>{},CancellationToken.None);
        Require(result.Completed==1 && result.Total==1 && !result.HasFailures,result.Message);
        Require((await converted.GetFilesAsync()).Single().SizeIsPositive(),"Empty video output.");
        var audio=await VideoService.Convert([video],converted,VideoEncodingQuality.HD720p,true,_=>{},CancellationToken.None);
        Require(audio.Completed==1 && audio.Total==1 && !audio.HasFailures,audio.Message);
        var music=(await converted.GetFilesAsync()).First(f=>f.FileType==".m4a");
        Require((await music.Properties.GetMusicPropertiesAsync()).Duration>TimeSpan.Zero,"Empty audio output.");
    });
    await Check("MP4 byte budgets preserve decoded video/audio and originals, reject impossible caps and clean up failures/cancellation",()=>VideoBudgetChecks.Run(folder));
    await Check("M4A byte budgets preserve decoded audio and originals, isolate failed inputs and clean up cancellation",()=>AudioBudgetChecks.Run(folder));
    await Check("Animated GIFs preserve changing frames, timing, loops and safe cancellation",async ()=>
    {
        var videos=await folder.CreateFolderAsync("gif-video"); var source=await videos.CreateFileAsync("animation.mp4");
        var composition=new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(global::Windows.UI.Color.FromArgb(255,230,20,20),TimeSpan.FromSeconds(1)));
        composition.Clips.Add(MediaClip.CreateFromColor(global::Windows.UI.Color.FromArgb(255,20,20,230),TimeSpan.FromSeconds(1)));
        var profile=MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Wvga); profile.Video.Width=160; profile.Video.Height=90;
        Require(await composition.RenderToFileAsync(source,MediaTrimmingPreference.Precise,profile)==TranscodeFailureReason.None,"GIF source encoder failed.");
        byte[] original=File.ReadAllBytes(source.Path); var outputs=await videos.CreateFolderAsync("outputs");
        var options=new GifOptions(0,2,160,5,true);
        string result=await GifService.Convert([source],outputs,options,new InlineProgress(_=>{}),CancellationToken.None);
        Require(result.StartsWith("Converted 1 of 1"),result);
        var gif=(await outputs.GetFilesAsync()).Single();
        using(var stream=await gif.OpenReadAsync())
        {
            var decoder=await BitmapDecoder.CreateAsync(stream);
            Require(decoder.DecoderInformation.CodecId==BitmapDecoder.GifDecoderId && decoder.FrameCount==10,"Expected ten GIF frames.");
            Require(decoder.PixelWidth==160 && decoder.PixelHeight==90,"GIF dimensions differ.");
            var first=await decoder.GetFrameAsync(0); var last=await decoder.GetFrameAsync(9);
            var a=(await first.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage)).DetachPixelData();
            var b=(await last.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Require(a[2]>a[0]+80 && b[0]>b[2]+80,"GIF repeats the first frame or changes color incorrectly.");
            var timing=await first.BitmapProperties.GetPropertiesAsync(new[]{"/grctlext/Delay"});
            Require(Convert.ToUInt16(timing["/grctlext/Delay"].Value)==20,"GIF timing differs.");
            var loop=await decoder.BitmapContainerProperties.GetPropertiesAsync(new[]{"/appext/application","/appext/data"});
            Require(System.Text.Encoding.ASCII.GetString((byte[])loop["/appext/application"].Value)=="NETSCAPE2.0" && ((byte[])loop["/appext/data"].Value).Take(4).SequenceEqual(new byte[]{3,1,0,0}),"GIF looping metadata differs.");
        }
        string trimmed=await GifService.Convert([source],outputs,new(1.2,.8,160,5,false),new InlineProgress(_=>{}),CancellationToken.None);
        Require(trimmed.StartsWith("Converted 1 of 1") && (await outputs.GetFilesAsync()).Count==2,"GIF name collision overwrote output.");
        var trimmedFile=(await outputs.GetFilesAsync()).Single(f=>f.Path!=gif.Path);
        using(var stream=await trimmedFile.OpenReadAsync())
        {
            var decoder=await BitmapDecoder.CreateAsync(stream); Require(decoder.FrameCount==4,"Trim duration differs.");
            byte[] pixel=(await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Require(pixel[0]>pixel[2]+80,"GIF start time was not applied.");
            Require(!System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(trimmedFile.Path)).Contains("NETSCAPE2.0"),"Loop disabled but infinite-loop extension remains.");
        }
        using(var cancel=new CancellationTokenSource())
        {
            try { await GifService.Convert([source],outputs,options,new InlineProgress(_=>cancel.Cancel()),cancel.Token); throw new InvalidOperationException("Canceled GIF completed."); }
            catch(OperationCanceledException) { }
        }
        Require((await outputs.GetFilesAsync()).Count==2,"Canceled GIF left an incomplete output.");
        string invalid=await GifService.Convert([source],outputs,new(50,1,160,5),new InlineProgress(_=>{}),CancellationToken.None);
        Require(invalid.StartsWith("Converted 0 of 1") && (await outputs.GetFilesAsync()).Count==2,"Out-of-range trim created an output.");
        Require(original.SequenceEqual(File.ReadAllBytes(source.Path)),"GIF conversion changed its source.");
    });
}
finally { try { Directory.Delete(root,true); } catch(IOException) { } }
Console.WriteLine($"{passed} Windows integration checks passed; {failed} failed.");
return failed==0?0:1;

    }
}

internal static class TestFile
{ public static bool SizeIsPositive(this StorageFile file)=>new FileInfo(file.Path).Length>0; }

internal sealed class InlineProgress(Action<string> report) : IProgress<string>
{ public void Report(string value)=>report(value); }
