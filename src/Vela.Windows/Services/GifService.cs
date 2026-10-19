using Vela.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;

namespace Vela.Windows.Services;

internal sealed record GifOptions(double Start=0,double Duration=5,uint LongestEdge=640,int FramesPerSecond=10,bool Loop=true)
{
    public void Validate()
    {
        if(!double.IsFinite(Start) || Start<0 || !double.IsFinite(Duration) || Duration<=0 || Duration>30 ||
            LongestEdge is <160 or >1280 || FramesPerSecond is <5 or >20)
            throw new ArgumentOutOfRangeException(nameof(Duration),"Use a valid start time, up to 30 seconds, 160–1280 pixels and 5–20 frames per second.");
    }
}

internal static class GifService
{
    public static async Task<string> Convert(IReadOnlyList<StorageFile> files,StorageFolder folder,GifOptions options,IProgress<string> progress,CancellationToken cancellation)
    {
        options.Validate(); int completed=0; var failures=new List<string>();
        foreach(var file in files)
        {
            cancellation.ThrowIfCancellationRequested(); StorageFile? output=null;
            try
            {
                var clip=await MediaClip.CreateFromFileAsync(file).AsTask(cancellation);
                double remaining=clip.OriginalDuration.TotalSeconds-options.Start;
                if(remaining<=0) throw new InvalidOperationException("The start time is past the end of this video.");
                double duration=Math.Min(remaining,options.Duration);
                int frames=(int)Math.Ceiling(duration*options.FramesPerSecond);
                if(frames>300) throw new InvalidOperationException("Choose a shorter duration or lower frame rate: a GIF can contain up to 300 frames.");
                var video=clip.GetVideoEncodingProperties();
                var size=ImageSizing.Fit(video.Width,video.Height,options.LongestEdge);
                var composition=new MediaComposition(); composition.Clips.Add(clip);
                output=await folder.CreateFileAsync(Path.GetFileNameWithoutExtension(file.Name)+".gif",CreationCollisionOption.GenerateUniqueName).AsTask(cancellation);
                using var stream=await output.OpenAsync(FileAccessMode.ReadWrite).AsTask(cancellation);
                var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.GifEncoderId,stream).AsTask(cancellation);
                if(options.Loop)
                    await encoder.BitmapContainerProperties.SetPropertiesAsync(new Dictionary<string,BitmapTypedValue>
                    {
                        ["/appext/application"]=new(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"),PropertyType.UInt8Array),
                        ["/appext/data"]=new(new byte[]{3,1,0,0,0},PropertyType.UInt8Array)
                    }).AsTask(cancellation);
                for(int i=0;i<frames;i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    // Extract at an explicit source timestamp; thumbnail extraction can ignore clip trim metadata.
                    using var image=await composition.GetThumbnailAsync(TimeSpan.FromSeconds(options.Start+(double)i/options.FramesPerSecond),(int)size.Width,(int)size.Height,VideoFramePrecision.NearestFrame).AsTask(cancellation);
                    var decoder=await BitmapDecoder.CreateAsync(image).AsTask(cancellation);
                    using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore).AsTask(cancellation);
                    encoder.SetSoftwareBitmap(bitmap);
                    // Cumulative rounding keeps the total animation duration accurate at rates such as 15 fps.
                    int start=(int)Math.Round(i*100.0/options.FramesPerSecond);
                    int end=(int)Math.Round(Math.Min(duration,(i+1.0)/options.FramesPerSecond)*100);
                    await encoder.BitmapProperties.SetPropertiesAsync(new Dictionary<string,BitmapTypedValue>
                    {
                        ["/grctlext/Delay"]=new((ushort)Math.Max(1,end-start),PropertyType.UInt16),
                        ["/grctlext/Disposal"]=new((byte)1,PropertyType.UInt8),
                        ["/grctlext/TransparencyFlag"]=new(false,PropertyType.Boolean)
                    }).AsTask(cancellation);
                    progress.Report($"{file.Name} · frame {i+1} of {frames}");
                    if(i<frames-1) await encoder.GoToNextFrameAsync().AsTask(cancellation);
                    else await encoder.FlushAsync().AsTask(cancellation);
                    if(stream.Size>100_000_000) throw new InvalidOperationException("The GIF exceeds 100 MB. Choose a smaller size or shorter duration.");
                }
                cancellation.ThrowIfCancellationRequested(); completed++;
            }
            catch(OperationCanceledException) { if(output is not null) await output.DeleteAsync(); throw; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
            { if(output is not null) await output.DeleteAsync(); failures.Add($"{file.Name}: {ex.Message}"); }
        }
        return $"Converted {completed} of {files.Count} GIFs. Originals were kept."+(failures.Count==0?"":"\n"+string.Join("\n",failures.Take(8)));
    }
}
