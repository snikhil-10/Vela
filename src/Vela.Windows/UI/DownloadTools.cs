using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private void Downloads()
    {
        string? root=null;
        var path=Ui.Text("Choose the download folder used by your messaging app.","VelaCaption"); Body.Children.Add(path);
        var age=new NumberBox { Header="Unchanged for at least this many days",Minimum=1,Maximum=3650,Value=30,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact }; Body.Children.Add(age);
        var list=new ListView { DisplayMemberPath="Display",SelectionMode=ListViewSelectionMode.Multiple,MaxHeight=340 }; Body.Children.Add(list);
        Body.Children.Add(AsyncButton("Choose messaging download folder",async ()=>
        { if(conversion is not null) return; var chosen=await MediaService.PickFolder(WindowStyle.Handle(this)); if(chosen is not null) { root=chosen.Path; path.Text=root; list.ItemsSource=null; } }));
        Body.Children.Add(AsyncButton("Review older downloads",async ()=>
        {
            if(root is null) { Message("Choose a folder first.",true); return; }
            if(conversion is not null) return; conversion=new(); string scanned=root;
            try
            {
                int days=(int)(double.IsFinite(age.Value)?age.Value:30);
                var result=await Task.Run(()=>DownloadReview.Scan(scanned,days,conversion.Token));
                list.ItemsSource=result.Files; Message($"{result.Files.Count} older files. {result.Skipped} inaccessible or linked items skipped."+(result.Limited?" Scan limit reached; choose a smaller folder.":" Select only files you no longer need."));
            }
            finally { conversion.Dispose(); conversion=null; }
        },waitOnExit:true));
        Body.Children.Add(AsyncButton("Send selected files to Recycle Bin",async ()=>
        {
            if(root is null || list.SelectedItems.Count==0) { Message("Review the folder and select files first.",true); return; }
            var selected=list.SelectedItems.Cast<ReviewFile>().ToList();
            var confirm=new ContentDialog { Title="Recycle selected downloads?",Content=$"You selected {selected.Count} files in {root}. Files that changed after review will be skipped. Windows will ask if a file cannot be recycled.",PrimaryButtonText="Recycle selected",CloseButtonText="Keep files",DefaultButton=ContentDialogButton.Close,XamlRoot=Root.XamlRoot };
            if(await confirm.ShowAsync()!=ContentDialogResult.Primary) return;
            Message(await Task.Run(()=>DownloadReview.Recycle(root,selected))); list.ItemsSource=null;
        }));
        Body.Children.Add(Ui.Button("Cancel scan",()=>conversion?.Cancel()));
        Add("You choose what goes","Vela scans only the folder you pick and its ordinary subfolders. It does not read messages or identify where a file came from. Nothing is removed on a timer. Select an app-specific download folder to keep the review focused.");
    }
}
