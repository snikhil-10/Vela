using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Vela.Core;

namespace Vela.Windows.Services;

internal sealed record VideoConversionResult(int Completed,int Total,IReadOnlyList<string> Failures)
{
    public bool HasFailures=>Failures.Count>0;
    public string Message=>$"Converted {Completed} of {Total} files. Originals were kept."
        +(HasFailures?"\n"+string.Join("\n",Failures.Take(8)):"");
    public override string ToString()=>Message;
}

internal static class VideoService
{
    public static async Task<IReadOnlyList<StorageFile>> Pick(nint hwnd)
    {
        var picker=new FileOpenPicker(); foreach(string extension in new[]{".mp4",".m4v",".mov",".mkv",".wmv",".avi"}) picker.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(picker,hwnd); return await picker.PickMultipleFilesAsync();
    }
    public static async Task<VideoConversionResult> Convert(IReadOnlyList<StorageFile> files,StorageFolder folder,VideoEncodingQuality quality,bool audioOnly,Action<string> progress,CancellationToken cancellation,long maximumBytes=0)
    {
        if(maximumBytes is <0 or >VideoSizeBudget.MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        int completed=0,index=0; var failures=new List<string>();
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested(); index++;
            StorageFile? output=null;
            try
            {
                var profile=audioOnly?MediaEncodingProfile.CreateM4a(AudioEncodingQuality.High):MediaEncodingProfile.CreateMp4(quality);
                VideoBitrates? rates=null; uint? audioRate=null;
                MediaEncodingProfile? sourceProfile=null;
                if(audioOnly || maximumBytes>0) sourceProfile=await MediaEncodingProfile.CreateFromFileAsync(file).AsTask(cancellation);
                if(audioOnly && sourceProfile!.Audio is null) throw new InvalidOperationException("This file has no audio stream to extract.");
                if(maximumBytes>0)
                {
                    if(audioOnly)
                    {
                        var properties=await file.Properties.GetMusicPropertiesAsync().AsTask(cancellation);
                        var duration=properties.Duration;
                        if(duration<=TimeSpan.Zero) duration=(await file.Properties.GetVideoPropertiesAsync().AsTask(cancellation)).Duration;
                        if(profile.Audio is null || profile.Audio.ChannelCount is not (1 or 2)) throw new InvalidOperationException("Windows could not select a mono or stereo AAC profile for this size limit.");
                        if(duration<=TimeSpan.Zero) throw new InvalidOperationException("Windows could not determine this file's audio duration.");
                        audioRate=AudioSizeBudget.Plan(maximumBytes,duration,profile.Audio.Bitrate);
                        if(audioRate is null) throw TooLarge(true);
                    }
                    else
                    {
                        if(sourceProfile!.Audio is null) profile.Audio=null;
                        var properties=await file.Properties.GetVideoPropertiesAsync().AsTask(cancellation);
                        if(properties.Duration<=TimeSpan.Zero || profile.Video.Bitrate==0) throw new InvalidOperationException("Windows could not determine a duration and fixed video quality for this file.");
                        rates=VideoSizeBudget.Plan(maximumBytes,properties.Duration,profile.Video.Bitrate,profile.Audio?.Bitrate??0);
                        if(rates is null) throw TooLarge(false);
                    }
                }
                for(int attempt=1;;attempt++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(rates is {} chosen)
                    { profile.Video.Bitrate=chosen.Video; if(profile.Audio is not null) profile.Audio.Bitrate=chosen.Audio; }
                    if(audioRate is {} chosenAudio) profile.Audio!.Bitrate=chosenAudio;
                    output=await folder.CreateFileAsync(Path.GetFileNameWithoutExtension(file.Name)+(audioOnly?".m4a":".mp4"),CreationCollisionOption.GenerateUniqueName);
                    var transcoder=new MediaTranscoder { AlwaysReencode=maximumBytes>0 };
                    var job=await transcoder.PrepareFileTranscodeAsync(file,output,profile).AsTask(cancellation);
                    if(!job.CanTranscode) throw new InvalidOperationException($"Windows codec: {job.FailureReason}");
                    progress($"Starting {index}/{files.Count} · {file.Name}"+(maximumBytes>0?$" · compression pass {attempt}/{VideoSizeBudget.MaximumAttempts}":""));
                    cancellation.ThrowIfCancellationRequested();
                    await job.TranscodeAsync().AsTask(cancellation,new Progress<double>(percent=>progress($"{file.Name} · {percent:F0}%")));
                    cancellation.ThrowIfCancellationRequested();
                    if(maximumBytes==0) break;
                    long bytes=checked((long)(await output.GetBasicPropertiesAsync().AsTask(cancellation)).Size);
                    if(bytes<=0) throw new InvalidOperationException("Windows produced an empty media file.");
                    if(bytes<=maximumBytes) break;
                    if(audioOnly)
                    {
                        audioRate=attempt<AudioSizeBudget.MaximumAttempts ? AudioSizeBudget.Lower(audioRate!.Value,maximumBytes,bytes) : null;
                        if(audioRate is null) throw TooLarge(true);
                    }
                    else
                    {
                        rates=attempt<VideoSizeBudget.MaximumAttempts ? VideoSizeBudget.Lower(rates!.Value,maximumBytes,bytes) : null;
                        if(rates is null) throw TooLarge(false);
                    }
                    await output.DeleteAsync(); output=null;
                }
                cancellation.ThrowIfCancellationRequested();
                completed++;
            }
            catch(OperationCanceledException) { if(output is not null) await output.DeleteAsync(); throw; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            { if(output is not null) await output.DeleteAsync(); failures.Add($"{file.Name}: {ex.Message}"); }
        }
        return new(completed,files.Count,failures.AsReadOnly());
    }
    private static InvalidOperationException TooLarge(bool audioOnly)=>new(audioOnly
        ? "Windows could not fit this audio within the M4A size limit. Increase the limit or use 0 for no limit. No oversized copy was kept."
        : "Windows could not fit this video within the MP4 size limit. Increase the limit or choose a smaller resolution. No oversized copy was kept.");
}
