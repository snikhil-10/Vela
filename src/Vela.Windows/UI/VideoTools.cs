using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Vela.Windows.Services;
using Vela.Core;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private void Videos()
    {
        IReadOnlyList<StorageFile> selected=[]; StorageFolder? destination=null;
        var files=Ui.Text("No videos selected.","VelaCaption"); var folder=Ui.Text("Choose an output folder.","VelaCaption");
        var choose=AsyncButton("Choose videos",async ()=> { if(conversion is not null) return; selected=await VideoService.Pick(WindowStyle.Handle(this)); files.Text=$"{selected.Count} videos selected"; }); Body.Children.Add(choose); Body.Children.Add(files);
        var output=AsyncButton("Choose output folder",async ()=> { if(conversion is not null) return; destination=await MediaService.PickFolder(WindowStyle.Handle(this)); folder.Text=destination?.Path??"Choose an output folder."; }); Body.Children.Add(output); Body.Children.Add(folder);
        var format=new ComboBox { Header="Batch output",ItemsSource=new[]{"MP4 · 720p","MP4 · 1080p","MP4 · 2160p","Audio only · M4A","Animated GIF"},SelectedIndex=1,HorizontalAlignment=HorizontalAlignment.Stretch }; Body.Children.Add(format);
        var sizeLimit=new NumberBox { Header="MP4 size limit in MiB (0 = no limit)",Minimum=0,Maximum=VideoSizeBudget.MaximumBytes/(1024*1024),Value=0,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact }; Body.Children.Add(sizeLimit);
        Body.Children.Add(Ui.Text("The limit applies to each MP4 and can lower picture and sound quality at the selected resolution. Compression may take several passes. Files that cannot fit are reported as failed; no oversized copy is kept. Originals stay unchanged.","VelaCaption"));
        var gif=Ui.Stack(); gif.Visibility=Visibility.Collapsed;
        var start=Ui.Number(0,0,86400,_=>{}); start.Header="Start at (seconds)";
        var duration=Ui.Number(5,1,30,_=>{}); duration.Header="Duration (seconds)";
        var edge=Ui.Number(640,160,1280,_=>{}); edge.Header="Longest edge (pixels)";
        var rate=Ui.Number(10,5,20,_=>{}); rate.Header="Frames per second";
        foreach(var box in new[]{start,duration,edge,rate}) { box.Width=240; gif.Children.Add(box); }
        var loop=new CheckBox { Content="Loop continuously",IsChecked=true }; gif.Children.Add(loop);
        gif.Children.Add(Ui.Text("Up to 300 frames, 30 seconds and 100 MB per GIF. The same trim applies to every selected video; a shorter video ends naturally. GIF has no audio and uses a limited color palette.","VelaCaption")); Body.Children.Add(gif);
        format.SelectionChanged+=(_,_)=>
        { gif.Visibility=format.SelectedIndex==4?Visibility.Visible:Visibility.Collapsed; sizeLimit.IsEnabled=format.SelectedIndex<3 && conversion is null; };
        var progress=new ProgressRing { IsActive=false,Width=28,Height=28,HorizontalAlignment=HorizontalAlignment.Left }; Body.Children.Add(progress);
        Body.Children.Add(AsyncButton("Convert batch",async ()=>
        {
            if(conversion is not null) return;
            if(selected.Count==0 || destination is null) { Message("Choose videos and an output folder first.",true); return; }
            bool mp4=format.SelectedIndex<3;
            if(mp4 && (!double.IsFinite(sizeLimit.Value) || sizeLimit.Value<0 || sizeLimit.Value>2048 || sizeLimit.Value!=Math.Truncate(sizeLimit.Value)))
            { Message("Use a whole-number MP4 size limit from 0 to 2048 MiB.",true); return; }
            long maximumBytes=mp4?checked((long)sizeLimit.Value*1024*1024):0;
            var gifOptions=new GifOptions(double.IsFinite(start.Value)?start.Value:0,double.IsFinite(duration.Value)?duration.Value:5,(uint)(double.IsFinite(edge.Value)?edge.Value:640),(int)(double.IsFinite(rate.Value)?rate.Value:10),loop.IsChecked==true);
            if(format.SelectedIndex==4) gifOptions.Validate();
            conversion=new(); progress.IsActive=true; choose.IsEnabled=false; output.IsEnabled=false; format.IsEnabled=false; sizeLimit.IsEnabled=false;
            foreach(var box in new[]{start,duration,edge,rate}) box.IsEnabled=false; loop.IsEnabled=false;
            var quality=format.SelectedIndex switch { 0=>VideoEncodingQuality.HD720p,2=>VideoEncodingQuality.Uhd2160p,_=>VideoEncodingQuality.HD1080p };
            try
            {
                if(format.SelectedIndex==4)
                {
                    var updates=new Progress<string>(text=>files.Text=text); var token=conversion.Token;
                    Message(await Task.Run(()=>GifService.Convert(selected,destination,gifOptions,updates,token),token));
                }
                else
                {
                    var result=await VideoService.Convert(selected,destination,quality,format.SelectedIndex==3,t=>files.Text=t,conversion.Token,maximumBytes);
                    Message(result.Message,result.HasFailures);
                }
            }
            finally { progress.IsActive=false; conversion.Dispose(); conversion=null; choose.IsEnabled=true; output.IsEnabled=true; format.IsEnabled=true; sizeLimit.IsEnabled=format.SelectedIndex<3; foreach(var box in new[]{start,duration,edge,rate}) box.IsEnabled=true; loop.IsEnabled=true; }
        },waitOnExit:true));
        Body.Children.Add(Ui.Button("Cancel",()=>conversion?.Cancel()));
        Add("Local batch processing","Windows Media Foundation converts each selected file into a new file with a unique name. Original media stays intact. Codec, DRM and format support depend on Windows; failed files are reported and incomplete outputs are removed.");
    }
}
