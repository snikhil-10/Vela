using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Vela.Windows.Services;

internal static class VideoService
{
    public static async Task<IReadOnlyList<StorageFile>> Pick(nint hwnd)
    {
        var picker=new FileOpenPicker(); foreach(string extension in new[]{".mp4",".m4v",".mov",".mkv",".wmv",".avi"}) picker.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(picker,hwnd); return await picker.PickMultipleFilesAsync();
    }
    public static async Task<string> Convert(IReadOnlyList<StorageFile> files,StorageFolder folder,VideoEncodingQuality quality,bool audioOnly,Action<string> progress,CancellationToken cancellation)
    {
        int completed=0; var failures=new List<string>();
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            StorageFile? output=null;
            try
            {
                var profile=audioOnly?MediaEncodingProfile.CreateM4a(AudioEncodingQuality.High):MediaEncodingProfile.CreateMp4(quality);
                output=await folder.CreateFileAsync(Path.GetFileNameWithoutExtension(file.Name)+(audioOnly?".m4a":".mp4"),CreationCollisionOption.GenerateUniqueName);
                var transcoder=new MediaTranscoder();
                var job=await transcoder.PrepareFileTranscodeAsync(file,output,profile).AsTask(cancellation);
                if(!job.CanTranscode) throw new InvalidOperationException($"Windows codec: {job.FailureReason}");
                progress($"{completed+1}/{files.Count} · {file.Name}");
                await job.TranscodeAsync().AsTask(cancellation,new Progress<double>(percent=>progress($"{file.Name} · {percent:F0}%")));
                completed++;
            }
            catch(OperationCanceledException) { if(output is not null) await output.DeleteAsync(); throw; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            { if(output is not null) await output.DeleteAsync(); failures.Add($"{file.Name}: {ex.Message}"); }
        }
        return $"Converted {completed} of {files.Count} files. Originals were kept."+(failures.Count==0?"":"\n"+string.Join("\n",failures.Take(8)));
    }
}
