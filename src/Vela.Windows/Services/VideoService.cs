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
        if(audioOnly && maximumBytes>0) throw new InvalidOperationException("File-size limits are available for MP4 output only.");
        int completed=0,index=0; var failures=new List<string>();
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested(); index++;
            StorageFile? output=null;
            try
            {
                var profile=audioOnly?MediaEncodingProfile.CreateM4a(AudioEncodingQuality.High):MediaEncodingProfile.CreateMp4(quality);
                VideoBitrates? rates=null;
                if(maximumBytes>0)
                {
                    var sourceProfile=await MediaEncodingProfile.CreateFromFileAsync(file).AsTask(cancellation);
                    if(sourceProfile.Audio is null) profile.Audio=null;
                    var properties=await file.Properties.GetVideoPropertiesAsync().AsTask(cancellation);
                    if(properties.Duration<=TimeSpan.Zero || profile.Video.Bitrate==0) throw new InvalidOperationException("Windows could not determine a duration and fixed video quality for this file.");
                    rates=VideoSizeBudget.Plan(maximumBytes,properties.Duration,profile.Video.Bitrate,profile.Audio?.Bitrate??0);
                    if(rates is null) throw TooLarge();
                }
                for(int attempt=1;;attempt++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(rates is {} chosen)
                    { profile.Video.Bitrate=chosen.Video; if(profile.Audio is not null) profile.Audio.Bitrate=chosen.Audio; }
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
                    if(bytes<=0) throw new InvalidOperationException("Windows produced an empty video file.");
                    if(bytes<=maximumBytes) break;
                    rates=attempt<VideoSizeBudget.MaximumAttempts ? VideoSizeBudget.Lower(rates!.Value,maximumBytes,bytes) : null;
                    if(rates is null) throw TooLarge();
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
    private static InvalidOperationException TooLarge()=>new("Windows could not fit this video within the MP4 size limit. Increase the limit or choose a smaller resolution. No oversized copy was kept.");
}
