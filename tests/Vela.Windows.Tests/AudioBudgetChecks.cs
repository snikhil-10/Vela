using Vela.Core;
using Vela.Windows.Services;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

internal static class AudioBudgetChecks
{
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    internal static async Task Run(StorageFolder root)
    {
        var working=await root.CreateFolderAsync("m4a-budgets");
        string path=Path.Combine(working.Path,"generated.wav");
        const int sampleRate=48000,seconds=12,samples=sampleRate*seconds,payload=samples*4;
        var random=new Random(104729);
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+payload);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)2); writer.Write(sampleRate); writer.Write(sampleRate*4);
            writer.Write((short)4); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(payload);
            for(int i=0;i<samples;i++)
            {
                double time=i/(double)sampleRate;
                writer.Write((short)(Math.Sin(time*2*Math.PI*440)*8000+random.Next(-3000,3000)));
                writer.Write((short)(Math.Sin(time*2*Math.PI*660)*8000+random.Next(-3000,3000)));
            }
        }
        var wave=await StorageFile.GetFileFromPathAsync(path); byte[] waveOriginal=File.ReadAllBytes(path);
        var source=await working.CreateFileAsync("generated.mp4"); var video=new MediaComposition();
        video.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,128,64,32),TimeSpan.FromSeconds(seconds)));
        video.BackgroundAudioTracks.Add(await BackgroundAudioTrack.CreateFromFileAsync(wave));
        var videoProfile=MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
        videoProfile.Video.Width=160; videoProfile.Video.Height=90; videoProfile.Video.Bitrate=256000;
        Require(await video.RenderToFileAsync(source,MediaTrimmingPreference.Precise,videoProfile)==TranscodeFailureReason.None,"Audio-budget source video render failed.");
        byte[] original=File.ReadAllBytes(source.Path);
        var outputs=await working.CreateFolderAsync("outputs");
        var baselineResult=await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,true,_=>{},default);
        Require(baselineResult.Completed==1 && !baselineResult.HasFailures,baselineResult.Message);
        var baseline=(await outputs.GetFilesAsync()).Single(); byte[] baselineOriginal=File.ReadAllBytes(baseline.Path);
        long baselineBytes=baselineOriginal.Length,limit=baselineBytes*3/4; int passes=0;
        var result=await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,true,message=> { if(message.StartsWith("Starting")) passes++; },default,limit);
        Require(result.Completed==1 && !result.HasFailures,result.Message);
        var capped=(await outputs.GetFilesAsync()).Single(file=>file.Path!=baseline.Path);
        long actual=new FileInfo(capped.Path).Length;
        var profile=await MediaEncodingProfile.CreateFromFileAsync(capped);
        var properties=await capped.Properties.GetMusicPropertiesAsync();
        Console.WriteLine($"INFO M4A budget: baseline={baselineBytes}; limit={limit}; capped={actual}; passes={passes}; bitrate={profile.Audio?.Bitrate}; channels={profile.Audio?.ChannelCount}; sampleRate={profile.Audio?.SampleRate}; duration={properties.Duration.TotalSeconds:F3}s.");
        Require(passes is >=1 and <=AudioSizeBudget.MaximumAttempts && actual>0 && actual<=limit && actual<baselineBytes,"M4A copy exceeds its cap or retry bound.");
        Require(profile.Video is null && profile.Audio is { ChannelCount:2 } && properties.Duration.TotalSeconds is >11.8 and <12.2,"Capped M4A lost its stream, channels or duration.");
        var decoded=await working.CreateFileAsync("decoded.wav");
        var decoder=new MediaTranscoder { AlwaysReencode=true };
        var job=await decoder.PrepareFileTranscodeAsync(capped,decoded,MediaEncodingProfile.CreateWav(AudioEncodingQuality.High));
        Require(job.CanTranscode,"Capped M4A could not be prepared for PCM decoding."); await job.TranscodeAsync();
        using(var reader=new BinaryReader(File.OpenRead(decoded.Path)))
        {
            Require(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4))=="RIFF","Decoded audio is not a RIFF container.");
            reader.ReadUInt32(); Require(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4))=="WAVE","Decoded output is not WAV.");
            bool audible=false;
            while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
            {
                string id=System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)); uint length=reader.ReadUInt32();
                if(id=="data") { audible=reader.ReadBytes((int)Math.Min(length,65536u)).Any(value=>value!=0); break; }
                reader.BaseStream.Seek(length+(length&1),SeekOrigin.Current);
            }
            Require(audible,"Capped M4A decoded to empty or silent PCM.");
        }
        var impossible=await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,true,_=>{},default,1);
        Require(impossible.Completed==0 && impossible.HasFailures && impossible.Message.Contains("M4A size limit") && (await outputs.GetFilesAsync()).Count==2,"Impossible audio cap retained a copy or hid its failure.");
        var waveOutput=await working.CreateFolderAsync("wave-output");
        var waveResult=await VideoService.Convert([wave],waveOutput,VideoEncodingQuality.HD720p,true,_=>{},default,limit);
        Require(waveResult.Completed==1 && !waveResult.HasFailures && new FileInfo((await waveOutput.GetFilesAsync()).Single().Path).Length<=limit,"PCM input did not respect its M4A cap.");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel(); bool observed=false;
            try { await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,true,_=>{},canceled.Token,limit); }
            catch(OperationCanceledException) { observed=true; }
            Require(observed && (await outputs.GetFilesAsync()).Count==2,"Pre-canceled M4A conversion created output.");
        }
        using(var stop=new CancellationTokenSource())
        {
            bool observed=false;
            try { await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,true,message=> { if(message.StartsWith("Starting")) stop.Cancel(); },stop.Token,limit); }
            catch(OperationCanceledException) { observed=true; }
            Require(observed && (await outputs.GetFilesAsync()).Count==2,"Cancellation after preparing M4A kept a partial copy.");
        }
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false,observed=false,accept=true; object gate=new();
            try
            {
                await VideoService.Convert([source],outputs,VideoEncodingQuality.HD720p,true,message=>
                { lock(gate) if(accept && message.Contains('%')) { accept=false; observed=true; stop.Cancel(); } },stop.Token,limit);
            }
            catch(OperationCanceledException) { canceled=true; }
            finally { lock(gate) accept=false; }
            Require(observed && canceled && (await outputs.GetFilesAsync()).Count==2,"Active audio encoder cancellation was not exercised or left output.");
            Console.WriteLine("INFO M4A cancellation during native encoder progress removed its partial copy.");
        }
        var silent=await working.CreateFileAsync("no-audio.mp4"); var composition=new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,32,64,128),TimeSpan.FromSeconds(1)));
        var silentProfile=MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Wvga); silentProfile.Audio=null;
        silentProfile.Video.Width=160; silentProfile.Video.Height=90;
        Require(await composition.RenderToFileAsync(silent,MediaTrimmingPreference.Precise,silentProfile)==TranscodeFailureReason.None,"No-audio fixture render failed.");
        byte[] silentOriginal=File.ReadAllBytes(silent.Path);
        foreach(long cap in new[]{0L,limit})
        {
            var noAudio=await VideoService.Convert([silent],outputs,VideoEncodingQuality.HD720p,true,_=>{},default,cap);
            Require(noAudio.Completed==0 && noAudio.Failures.Count==1 && noAudio.Message.Contains("no audio stream") && (await outputs.GetFilesAsync()).Count==2,"A missing audio stream was accepted or left output.");
        }
        var broken=await working.CreateFileAsync("broken.mp4"); await FileIO.WriteTextAsync(broken,"Not media");
        var mixed=await VideoService.Convert([broken,silent,source],outputs,VideoEncodingQuality.HD720p,true,_=>{},default,limit);
        Require(mixed.Completed==1 && mixed.Total==3 && mixed.Failures.Count==2 && mixed.Message.Contains("no audio stream") && mixed.Message.Contains("broken.mp4") && (await outputs.GetFilesAsync()).Count==3,"Mixed M4A batch stopped early, hid a failure or retained partial output.");
        Require(original.SequenceEqual(File.ReadAllBytes(source.Path)) && waveOriginal.SequenceEqual(File.ReadAllBytes(wave.Path)) && silentOriginal.SequenceEqual(File.ReadAllBytes(silent.Path)) && baselineOriginal.SequenceEqual(File.ReadAllBytes(baseline.Path)),"Audio conversion changed a source or earlier output.");
        Require((await outputs.GetFilesAsync()).All(file=>file.FileType==".m4a" && new FileInfo(file.Path).Length>0),"Audio batch left an unexpected or empty output.");
    }
}
