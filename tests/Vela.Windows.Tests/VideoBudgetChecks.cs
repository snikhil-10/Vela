using System.Runtime.InteropServices.WindowsRuntime;
using Vela.Windows.Services;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

internal static class VideoBudgetChecks
{
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    internal static async Task Run(StorageFolder root)
    {
        var working=await root.CreateFolderAsync("mp4-budgets");
        var source=await working.CreateFileAsync("source.mp4");
        var composition=new MediaComposition(); var random=new Random(1729);
        for(int frame=0;frame<8;frame++)
        {
            var image=await working.CreateFileAsync($"noise-{frame}.bmp");
            byte[] pixels=new byte[320*180*4]; random.NextBytes(pixels);
            for(int i=3;i<pixels.Length;i+=4) pixels[i]=255;
            using(var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,320,180,BitmapAlphaMode.Ignore))
            using(var stream=await image.OpenAsync(FileAccessMode.ReadWrite))
            {
                bitmap.CopyFromBuffer(pixels.AsBuffer());
                var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId,stream);
                encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
            }
            composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(image,TimeSpan.FromSeconds(.5)));
        }
        string wave=Path.Combine(working.Path,"tone.wav");
        using(var writer=new BinaryWriter(File.Create(wave)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+384000);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(48000); writer.Write(96000);
            writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(384000);
            for(int i=0;i<192000;i++) writer.Write((short)(Math.Sin(i*2*Math.PI*440/48000)*4000));
        }
        composition.BackgroundAudioTracks.Add(await BackgroundAudioTrack.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(wave)));
        var profile=MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
        profile.Video.Width=640; profile.Video.Height=360; profile.Video.Bitrate=4000000;
        Require(await composition.RenderToFileAsync(source,MediaTrimmingPreference.Precise,profile)==TranscodeFailureReason.None,"Budget source encoder failed.");
        byte[] original=File.ReadAllBytes(source.Path);
        var outputs=await working.CreateFolderAsync("outputs");
        var baselineResult=await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,false,_=>{},default);
        Require(baselineResult.Completed==1 && !baselineResult.HasFailures,baselineResult.Message);
        var baseline=(await outputs.GetFilesAsync()).Single(); long baselineBytes=new FileInfo(baseline.Path).Length;
        long limit=Math.Max(65536,baselineBytes*3/4);
        Require(limit<baselineBytes,"Noise fixture does not exercise video compression.");
        var result=await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,false,_=>{},default,limit);
        Require(result.Completed==1 && !result.HasFailures,result.Message);
        var capped=(await outputs.GetFilesAsync()).Single(file=>file.Path!=baseline.Path);
        long actual=new FileInfo(capped.Path).Length;
        Console.WriteLine($"INFO MP4 budget: baseline={baselineBytes} bytes; limit={limit}; capped={actual}.");
        Require(actual>0 && actual<=limit,"MP4 copy exceeds its actual byte cap.");
        var properties=await capped.Properties.GetVideoPropertiesAsync();
        Require(properties.Duration.TotalSeconds is >3.8 and <4.2 && properties.Width>0 && properties.Height>0,"Capped MP4 lost its duration or dimensions.");
        var decoded=new MediaComposition(); decoded.Clips.Add(await MediaClip.CreateFromFileAsync(capped));
        using(var thumbnail=await decoded.GetThumbnailAsync(TimeSpan.FromSeconds(2),160,90,VideoFramePrecision.NearestFrame))
        { var decoder=await BitmapDecoder.CreateAsync(thumbnail); Require(decoder.PixelWidth>0 && decoder.PixelHeight>0,"Capped MP4 did not decode a frame."); }
        var audio=await working.CreateFolderAsync("audio");
        var sound=await VideoService.Convert([capped],audio,VideoEncodingQuality.HD720p,true,_=>{},default);
        Require(sound.Completed==1 && !sound.HasFailures && (await (await audio.GetFilesAsync()).Single().Properties.GetMusicPropertiesAsync()).Duration.TotalSeconds>3.8,"Capped MP4 lost its audio.");
        var impossible=await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,false,_=>{},default,1);
        Require(impossible.Completed==0 && impossible.HasFailures && (await outputs.GetFilesAsync()).Count==2,"Impossible video cap kept an incomplete copy.");
        var broken=await working.CreateFileAsync("broken.mp4"); await FileIO.WriteTextAsync(broken,"Not a video fixture");
        var mixed=await VideoService.Convert([broken,source],outputs,VideoEncodingQuality.HD720p,false,_=>{},default,limit);
        Require(mixed.Completed==1 && mixed.Total==2 && mixed.HasFailures && mixed.Failures.Count==1 && mixed.Message.Contains("broken.mp4") && (await outputs.GetFilesAsync()).Count==3,"A failed file stopped the remaining batch, hid a failure or overwrote a copy.");
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false;
            try { await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,false,message=> { if(message.StartsWith("Starting")) stop.Cancel(); },stop.Token,limit); }
            catch(OperationCanceledException) { canceled=true; }
            Require(canceled && (await outputs.GetFilesAsync()).Count==3,"Cancellation after preparing the encoder kept a partial copy.");
        }
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false,observed=false,accept=true; object gate=new();
            try
            {
                await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,false,message=>
                {
                    lock(gate) if(accept && message.Contains('%')) { accept=false; observed=true; stop.Cancel(); }
                },stop.Token,limit);
            }
            catch(OperationCanceledException) { canceled=true; }
            finally { lock(gate) accept=false; }
            Require(observed && canceled && (await outputs.GetFilesAsync()).Count==3,"Cancellation during native progress kept a partial copy or was not exercised.");
            Console.WriteLine("INFO MP4 cancellation during native encoder progress removed its partial copy.");
        }
        var beforeStrict=(await outputs.GetFilesAsync()).Select(file=>file.Path).ToHashSet(); int strictPasses=0;
        var strict=await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,false,message=>
        { if(message.StartsWith("Starting")) strictPasses++; },default,131072);
        var strictCopies=(await outputs.GetFilesAsync()).Where(file=>!beforeStrict.Contains(file.Path)).ToArray();
        Require(strictPasses is >=1 and <=3 && strictCopies.Length<=1 && strictCopies.All(file=>new FileInfo(file.Path).Length<=131072),"Strict cap exceeded the retry bound or left oversized output.");
        Require(strict.Completed==strictCopies.Length && strict.Total==1 && strict.HasFailures==(strictCopies.Length==0),"Strict budget result differs from retained copies.");
        Console.WriteLine($"INFO MP4 strict cap: limit=131072; passes={strictPasses}; retained={strictCopies.Length}; {strict}");
        var silent=await working.CreateFileAsync("silent.mp4"); var silentComposition=new MediaComposition();
        silentComposition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,20,80,160),TimeSpan.FromSeconds(4)));
        var silentProfile=MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Wvga); silentProfile.Audio=null;
        silentProfile.Video.Width=160; silentProfile.Video.Height=90;
        Require(await silentComposition.RenderToFileAsync(silent,MediaTrimmingPreference.Precise,silentProfile)==TranscodeFailureReason.None,"Silent fixture rendering failed.");
        Require((await MediaEncodingProfile.CreateFromFileAsync(silent)).Audio is null,"Silent fixture unexpectedly has an audio stream.");
        var silentOutputs=await working.CreateFolderAsync("silent-outputs");
        var silentResult=await VideoService.Convert([silent],silentOutputs,VideoEncodingQuality.HD720p,false,_=>{},default,65536);
        Require(silentResult.Completed==1 && !silentResult.HasFailures,silentResult.Message);
        var silentCopy=(await silentOutputs.GetFilesAsync()).Single();
        Require(new FileInfo(silentCopy.Path).Length<=65536 && (await MediaEncodingProfile.CreateFromFileAsync(silentCopy)).Audio is null,"Silent compression invented audio or exceeded its cap.");
        Require(original.SequenceEqual(File.ReadAllBytes(source.Path)),"MP4 compression changed the source bytes.");
    }
}
