using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Vela.Core;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow : Window
{
    private readonly AppController controller;
    internal string FeatureId { get; }
    private CancellationTokenSource? conversion;
    private Action? refreshCommands;
    internal ToolWindow(AppController controller,string featureId)
    {
        this.controller=controller; FeatureId=featureId; InitializeComponent(); WindowStyle.Configure(this,640,740);
        var feature=FeatureCatalog.Get(featureId); Heading.Text=feature.Name; Description.Text=feature.Description;
        switch(featureId)
        {
            case "monitorBrightness": Brightness(); break;
            case "thermalAlerts": Thermal(); break;
            case "keepAwake": Awake(); break;
            case "urlCleaner": Links(); break;
            case "shelf": Shelf(); break;
            case "textSnippets": Snippets(); break;
            case "clipboardGuard": ClipboardGuard(); break;
            case "pasteImage": PasteImage(); break;
            case "cleaningMode": Cleaning(); break;
            case "commandBar": Commands(); break;
            case "radialMenu":
                Heading.Text="Launchers"; Description.Text="Organize apps, files and websites into profiles and submenus.";
                Launchers(); break;
            case "displayDimmer": Displays(); break;
            case "resourceAlerts": Alerts(); break;
            case "messagingDownloads": Downloads(); break;
            case "videoTools": Videos(); break;
            case "mediaTools": Media(); break;
            case "micMute": Microphones(); break;
            case "pastePlain": PlainPaste(); break;
            case "windowSearch": WindowSearch(); break;
            case "savedLayouts": SavedLayouts(); break;
            case "internetSpeed": InternetSpeed(); break;
            case "imagePins": PinImages(); break;
        }
        Closed+=(_,_)=> { conversion?.Cancel(); if(featureId=="cleaningMode") { controller.Input.CleaningChanged-=CleaningStatus; controller.Input.EndCleaning(); } };
    }
    private void Message(string text,bool error=false) { Status.Message=text; Status.Severity=error?InfoBarSeverity.Error:InfoBarSeverity.Success; Status.IsOpen=true; }
    private Button AsyncButton(string text,Func<Task> action,bool waitOnExit=false)
    {
        var button=new Button { Content=text };
        async Task Perform()
        { Status.IsOpen=false; button.IsEnabled=false; try { await action(); } catch(OperationCanceledException) { Message("Canceled."); } catch(Exception ex) { Message(ex.Message,true); } finally { button.IsEnabled=true; } }
        button.Click+=async (_,_)=> { if(controller.Quitting) return; if(waitOnExit) await controller.RunOperation(Perform); else await Perform(); };
        return button;
    }
    internal void CancelWork() { conversion?.Cancel(); shelfAdds?.Cancel(); }
    private void Add(string title,string detail,UIElement? control=null)=>Body.Children.Add(Ui.Card(title,detail,control));
    private void Awake()
    {
        var duration=Ui.Number(60,1,1440,_=>{}); duration.Header="Minutes"; Body.Children.Add(duration);
        Body.Children.Add(Ui.Button("Start keep awake",()=> { controller.Awake.Hold(TimeSpan.FromMinutes(duration.Value)); Message(controller.Awake.Status); },true));
        Body.Children.Add(Ui.Button("Stop",()=> { controller.Awake.Release(); Message("Keep-awake timer stopped."); }));
        Add("Keep the display on","Otherwise the screen can turn off while your work continues.",Ui.Toggle(controller.Settings.KeepDisplayAwake,on=> { controller.Settings.KeepDisplayAwake=on; controller.Save(false); controller.Awake.Evaluate(); }));
        Add("Only while plugged in","Pause requests on battery power.",Ui.Toggle(controller.Settings.AwakeOnlyOnAc,on=> { controller.Settings.AwakeOnlyOnAc=on; controller.Save(false); controller.Awake.Evaluate(); }));
        Add("While an external display is connected","Request wakefulness while another display is available. AC-only still applies.",Ui.Toggle(controller.Settings.AwakeWithExternalDisplay,on=> { controller.Settings.AwakeWithExternalDisplay=on; controller.Save(false); controller.Awake.Evaluate(); }));
        var apps=new TextBox { Header="Stay awake while these apps run",PlaceholderText="One process name per line, such as render.exe",Text=string.Join(Environment.NewLine,controller.Settings.AwakeApps),AcceptsReturn=true,MinHeight=100 };
        Body.Children.Add(apps); Body.Children.Add(Ui.Button("Save app rule",()=> { controller.Settings.AwakeApps=apps.Text.Split(['\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList(); controller.Save(false); controller.Awake.Evaluate(); Message("App rule saved."); }));
        Body.Children.Add(Ui.Text("Windows still controls manual Sleep, lid-close actions, and critical battery protection.","VelaCaption"));
    }
    private void Links()
    {
        var text=new TextBox { Header="Link",PlaceholderText="https://example.com/?utm_source=…",AcceptsReturn=true,MinHeight=120,TextWrapping=TextWrapping.Wrap };
        Body.Children.Add(text);
        Body.Children.Add(AsyncButton("Use clipboard",async ()=>text.Text=await ClipboardService.ReadText()));
        Body.Children.Add(Ui.Button("Clean and copy",()=>
        {
            if(!Uri.TryCreate(text.Text.Trim(),UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https")) { Message("Enter an HTTP or HTTPS link.",true); return; }
            string original=text.Text; text.Text=LinkCleaner.Clean(original); ClipboardService.WriteText(text.Text);
            Message(original==text.Text?"Copied. No known tracking parameters were removed.":"Tracking parameters removed. Clean link copied.");
        },true));
        Add("Clean copied links automatically","Only plain-text HTTP links are changed. Rich content and signed URLs are left intact.",Ui.Toggle(controller.Settings.AutoCleanLinks,on=> { controller.Settings.AutoCleanLinks=on; controller.Save(false); }));
    }
    private void ClipboardGuard()
    {
        Add("Clear after","Seconds since the clipboard last changed. Windows history is managed separately.",Ui.Number(controller.Settings.ClipboardClearSeconds,10,86400,n=> { controller.Settings.ClipboardClearSeconds=n; controller.Save(false); }));
        Add("Clear on lock","Clear the current clipboard when Windows locks.",Ui.Toggle(controller.Settings.ClearOnLock,on=> { controller.Settings.ClearOnLock=on; controller.Save(false); }));
        Add("Clear on sleep","Clear when Windows sends its suspend notification.",Ui.Toggle(controller.Settings.ClearOnSleep,on=> { controller.Settings.ClearOnSleep=on; controller.Save(false); }));
        Body.Children.Add(Ui.Text("Pinned or saved Win+V entries may remain. This tool cannot guarantee erasure from other apps, remote sessions, or Windows history.","VelaCaption"));
    }
    private void PasteImage()
    {
        Body.Children.Add(Ui.Text("Copy an image, choose Save as PNG, then pick its destination. The clipboard and original image stay intact."));
        Body.Children.Add(AsyncButton("Save as PNG",async ()=>Message(await MediaService.SaveClipboardImage(WindowStyle.Handle(this)))));
    }
    private void Snippets()
    {
        var search=new TextBox { PlaceholderText="Find a snippet" }; Body.Children.Add(search);
        var list=new ListView { DisplayMemberPath="Name",MaxHeight=160 }; Body.Children.Add(list);
        void Refresh()=>list.ItemsSource=controller.Settings.Snippets.Where(s=>(s.Name+s.Trigger+s.Text).Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToList();
        search.TextChanged+=(_,_)=>Refresh(); Refresh();
        var name=new TextBox { Header="Name" }; var trigger=new TextBox { Header="Trigger",PlaceholderText="sig, @email, or ;signature",MaxLength=64 };
        var body=new TextBox { Header="Text",AcceptsReturn=true,MinHeight=140,MaxLength=65536,TextWrapping=TextWrapping.Wrap };
        var activation=new ComboBox { Header="Expand",ItemsSource=new[]{"Immediately","After Space, Tab, or Enter"},SelectedIndex=1,HorizontalAlignment=HorizontalAlignment.Stretch };
        Body.Children.Add(name); Body.Children.Add(trigger); Body.Children.Add(activation); Body.Children.Add(body);
        list.SelectionChanged+=(_,_)=> { if(list.SelectedItem is Snippet s) { name.Text=s.Name; trigger.Text=s.Trigger; body.Text=s.Text; activation.SelectedIndex=s.Expansion=="afterDelimiter"?1:0; } };
        Body.Children.Add(Ui.Text("Use {date}, {time}, {datetime}, or {clipboard}. Use any trigger of 1–64 characters without whitespace. No prefix is required. Delayed expansion keeps the Space, Tab, or Enter after the replacement; the longest matching trigger wins.","VelaCaption"));
        var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        actions.Children.Add(Ui.Button("Save",()=>
        {
            if(string.IsNullOrWhiteSpace(name.Text)||!SnippetMatcher.ValidTrigger(trigger.Text)) { Message("Give the snippet a name and a trigger of 1–64 characters without whitespace or control characters.",true); return; }
            var previous=list.SelectedItem as Snippet;
            if(controller.Settings.Snippets.Any(s=>s.Trigger==trigger.Text && s!=previous)) { Message("That trigger is already in use.",true); return; }
            if(previous is not null) controller.Settings.Snippets.Remove(previous);
            controller.Settings.Snippets.Add(new(name.Text.Trim(),trigger.Text,body.Text,activation.SelectedIndex==1?"afterDelimiter":"immediate")); controller.Save(false); Refresh(); Message("Snippet saved.");
        },true));
        actions.Children.Add(AsyncButton("Copy expanded",async ()=>ClipboardService.WriteText(SnippetExpander.Expand(body.Text,DateTimeOffset.Now,await ClipboardService.ReadText()))));
        actions.Children.Add(Ui.Button("New",()=> { list.SelectedItem=null; name.Text=""; trigger.Text=""; body.Text=""; activation.SelectedIndex=1; }));
        actions.Children.Add(Ui.Button("Delete",()=> { if(list.SelectedItem is Snippet s) { controller.Settings.Snippets.Remove(s); controller.Save(false); Refresh(); } }));
        Body.Children.Add(actions);
    }
    private ToggleSwitch? lockCleaningMouse;
    private void Cleaning()
    {
        Body.Children.Add(Ui.Text("Pause input for up to 60 seconds. Hold Escape for two seconds to unlock early. The countdown continues even with the mouse locked.","VelaBody"));
        lockCleaningMouse=Ui.Toggle(controller.Settings.CleaningLockMouse,on=> { controller.Settings.CleaningLockMouse=on; controller.Save(false); });
        Add("Also lock the mouse","Block pointer movement, buttons and scrolling during the next cleaning session. Use Escape or wait for the countdown when the mouse is locked.",lockCleaningMouse);
        Body.Children.Add(Ui.Button("Start cleaning",()=> { controller.Input.BeginCleaning(); CleaningStatus(); },true));
        Body.Children.Add(Ui.Button("Stop",()=>controller.Input.EndCleaning()));
        controller.Input.CleaningChanged+=CleaningStatus; CleaningStatus();
    }
    private void CleaningStatus()
    {
        if(lockCleaningMouse is not null) lockCleaningMouse.IsEnabled=!controller.Input.Cleaning;
        Message(controller.Input.Cleaning
            ? $"{(controller.Input.CleaningMouse?"Keyboard and mouse":"Keyboard")} paused · {controller.Input.CleaningSeconds}s left. Hold Esc for two seconds to finish."
            : "Keyboard and mouse are available.");
    }
    private void Commands()
    {
        var search=new TextBox { PlaceholderText="Find a tool, snippet, or launcher" };
        var list=new ListView { MaxHeight=460,IsItemClickEnabled=true,DisplayMemberPath="Name" };
        Body.Children.Add(search); Body.Children.Add(list);
        void Invoke(CommandResult result)
        {
            if(controller.Quitting) return;
            Status.IsOpen=false;
            try { result.Action(); }
            catch(Exception) { Message($"Could not open “{result.Name}”. It may have moved or be unavailable. Check its target or access permissions.",true); }
        }
        void Refresh()
        {
            var results=new List<CommandResult>();
            results.AddRange(controller.OrderedFeatures.Where(f=>f.Id!="commandBar" && controller.Settings.EnabledFeatures.Contains(f.Id)).Select(f=>new CommandResult(f.Name,()=> { controller.OpenTool(f.Id); })));
            if(controller.Settings.EnabledFeatures.Contains("textSnippets")) results.AddRange(controller.Settings.Snippets.Select(s=>new CommandResult(s.Name+" · "+s.Trigger,()=> { ClipboardService.WriteText(SnippetExpander.Expand(s.Text,DateTimeOffset.Now)); Message("Snippet copied."); })));
            results.AddRange(LauncherTree.Leaves(controller.Settings.CurrentLaunchers).Select(l=>new CommandResult(l.Path,()=>Launch(l.Item))));
            if(controller.Settings.EnabledFeatures.Contains("windowSearch"))
                results.AddRange(controller.Workspaces.Windows().Select(w=>new CommandResult(w.Display,()=> { WindowWorkspace.Focus(w); Close(); })));
            if(controller.Settings.EnabledFeatures.Contains("savedLayouts"))
                results.AddRange(controller.Settings.WindowLayouts.Select(l=>new CommandResult("Workspace · "+l.Name,()=>Message(controller.Workspaces.Restore(l)))));
            list.ItemsSource=results.Where(r=>r.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToList();
            if(list.Items.Count>0) list.SelectedIndex=0;
        }
        search.TextChanged+=(_,_)=>Refresh();
        list.ItemClick+=(_,e)=> { if(e.ClickedItem is CommandResult result) Invoke(result); };
        search.KeyDown+=(_,e)=>
        {
            if(e.Key==global::Windows.System.VirtualKey.Enter && list.SelectedItem is CommandResult r) { e.Handled=true; Invoke(r); }
            if(e.Key==global::Windows.System.VirtualKey.Down && list.Items.Count>0) { e.Handled=true; list.SelectedIndex=Math.Min(list.Items.Count-1,list.SelectedIndex+1); }
            if(e.Key==global::Windows.System.VirtualKey.Up && list.Items.Count>0) { e.Handled=true; list.SelectedIndex=Math.Max(0,list.SelectedIndex-1); }
        };
        refreshCommands=Refresh; Refresh(); search.Loaded+=(_,_)=>search.Focus(FocusState.Programmatic);
        Launchers();
    }
    internal static void Launch(Launcher launcher)
    {
        if(launcher.IsFolder || !Launcher.IsAllowedTarget(launcher.Target)) return;
        Process.Start(new ProcessStartInfo(launcher.Target) { UseShellExecute=true });
    }
    private void Displays()
    {
        foreach(var display in controller.Dimmer.Displays())
        {
            var slider=new Slider { Minimum=0,Maximum=70,StepFrequency=1,Header=display.Name,Value=0 };
            slider.ValueChanged+=(_,e)=>controller.Dimmer.Dim(display.Handle,(int)e.NewValue); Body.Children.Add(slider);
        }
        Body.Children.Add(Ui.Button("Restore all displays",()=> { controller.Dimmer.Stop(); foreach(var slider in Body.Children.OfType<Slider>()) slider.Value=0; }));
        Body.Children.Add(Ui.Text("This adds a dark overlay; it does not change monitor hardware settings. Dimming resets when Vela exits, the feature is disabled, or displays change.","VelaCaption"));
    }
    private void Alerts()
    {
        Add("CPU threshold","Alert after 30 seconds above this percentage.",Ui.Number(controller.Settings.CpuAlertPercent,10,100,n=> { controller.Settings.CpuAlertPercent=n; controller.Save(false); }));
        Add("Memory threshold","Alert after 30 seconds above this percentage.",Ui.Number(controller.Settings.MemoryAlertPercent,10,100,n=> { controller.Settings.MemoryAlertPercent=n; controller.Save(false); }));
        Add("Low disk space","Alert below this percentage of free space on the Windows drive.",Ui.Number(controller.Settings.DiskAlertPercent,1,50,n=> { controller.Settings.DiskAlertPercent=n; controller.Save(false); }));
        Add("Quiet by default","Each alert has a 15-minute cooldown and respects Windows quiet time. NVIDIA temperature alerts are available separately under GPU thermal alerts, when the installed driver supports them.");
    }
    private void Microphones()
    {
        var devices=Ui.Stack(8); Body.Children.Add(devices);
        void Refresh()
        {
            devices.Children.Clear();
            foreach(var device in MicrophoneService.List()) devices.Children.Add(Ui.Card(device.Name,device.Muted?"Muted":"Microphone available"));
            if(devices.Children.Count==0) devices.Children.Add(Ui.Text("No active recording devices found."));
        }
        Body.Children.Add(Ui.Button("Mute all microphones",()=> { try { MicrophoneService.SetAll(true); Refresh(); } catch(Exception ex) { Message(ex.Message,true); } },true));
        Body.Children.Add(Ui.Button("Unmute all microphones",()=> { try { MicrophoneService.SetAll(false); Refresh(); } catch(Exception ex) { Message(ex.Message,true); } }));
        Body.Children.Add(Ui.Button("Refresh",Refresh));
        Body.Children.Add(Ui.Text("This changes each device's Windows mute control. New devices are included the next time you use the buttons. Closing Vela does not change the mute state you chose.","VelaCaption"));
        try { Refresh(); } catch(Exception ex) { Message(ex.Message,true); }
    }
    private void Media()
    {
        IReadOnlyList<StorageFile> selected=[]; StorageFolder? destination=null;
        var files=Ui.Text("No images selected.","VelaCaption"); var folder=Ui.Text("Choose an output folder.","VelaCaption");
        Body.Children.Add(AsyncButton("Choose images",async ()=> { selected=await MediaService.PickImages(WindowStyle.Handle(this)); files.Text=$"{selected.Count} images selected"; }));
        Body.Children.Add(files);
        Body.Children.Add(AsyncButton("Choose output folder",async ()=> { destination=await MediaService.PickFolder(WindowStyle.Handle(this)); folder.Text=destination?.Path??"Choose an output folder."; }));
        Body.Children.Add(folder);
        var format=new ComboBox { Header="Output format",ItemsSource=new[]{".png",".jpg",".bmp"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch }; Body.Children.Add(format);
        var edge=new NumberBox { Header="Maximum long edge in pixels (0 keeps original size)",Minimum=0,Maximum=16384,Value=0,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact }; Body.Children.Add(edge);
        var quality=new Slider { Header="JPEG quality (maximum with a size limit)",Minimum=10,Maximum=100,Value=90,StepFrequency=1 }; Body.Children.Add(quality);
        var sizeLimit=new NumberBox { Header="JPEG size limit in KiB (0 = no limit)",Minimum=0,Maximum=102400,Value=0,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact,IsEnabled=false };
        Body.Children.Add(sizeLimit);
        Body.Children.Add(Ui.Text("The limit applies to each JPEG and can lower its quality. If an image cannot fit, reduce its maximum edge or increase the limit. Originals stay unchanged.","VelaCaption"));
        format.SelectionChanged+=(_,_)=> { bool jpeg=(string)format.SelectedItem==".jpg"; quality.IsEnabled=jpeg; sizeLimit.IsEnabled=jpeg; };
        quality.IsEnabled=false;
        var watermarkToggle=new CheckBox { Content="Add a text watermark" }; Body.Children.Add(watermarkToggle);
        var watermarkFields=Ui.Stack(8); watermarkFields.Visibility=Visibility.Collapsed; Body.Children.Add(watermarkFields);
        var watermarkText=new TextBox { Header="Watermark text",MaxLength=120,PlaceholderText="Your name or copyright notice" }; watermarkFields.Children.Add(watermarkText);
        var watermarkPosition=new ComboBox { Header="Position",ItemsSource=new[]{"Bottom right","Bottom left","Top right","Top left"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch }; watermarkFields.Children.Add(watermarkPosition);
        var watermarkOpacity=new Slider { Header="Watermark opacity",Minimum=10,Maximum=100,Value=65,StepFrequency=1 }; watermarkFields.Children.Add(watermarkOpacity);
        var watermarkSize=new NumberBox { Header="Text size (% of image long edge)",Minimum=1,Maximum=10,Value=3,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact }; watermarkFields.Children.Add(watermarkSize);
        watermarkFields.Children.Add(Ui.Text("White text is applied after resizing. Long text is shortened to fit. PNG keeps its transparent background.","VelaCaption"));
        watermarkToggle.Checked+=(_,_)=>watermarkFields.Visibility=Visibility.Visible;
        watermarkToggle.Unchecked+=(_,_)=>watermarkFields.Visibility=Visibility.Collapsed;
        var progress=new ProgressRing { IsActive=false,Width=28,Height=28,HorizontalAlignment=HorizontalAlignment.Left }; Body.Children.Add(progress);
        Body.Children.Add(AsyncButton("Convert images",async ()=>
        {
            if(conversion is not null) { Message("Wait for the current batch or cancel it first.",true); return; }
            if(selected.Count==0 || destination is null) { Message("Choose images and an output folder first.",true); return; }
            bool jpeg=(string)format.SelectedItem==".jpg";
            if(jpeg && (!double.IsFinite(sizeLimit.Value) || sizeLimit.Value<0 || sizeLimit.Value>102400 || sizeLimit.Value!=Math.Truncate(sizeLimit.Value)))
            { Message("Use a whole-number JPEG size limit from 0 to 102400 KiB.",true); return; }
            long maximumBytes=jpeg?checked((long)sizeLimit.Value*1024):0;
            ImageWatermark? watermark=null;
            if(watermarkToggle.IsChecked==true)
            {
                if(string.IsNullOrWhiteSpace(watermarkText.Text)) { Message("Enter watermark text before converting.",true); return; }
                watermark=new(watermarkText.Text,watermarkOpacity.Value/100,watermarkSize.Value,(string)watermarkPosition.SelectedItem);
            }
            conversion=new(); progress.IsActive=true;
            try { int count=await MediaService.Convert(selected,destination,(string)format.SelectedItem,t=>files.Text=t,conversion.Token,(uint)(double.IsFinite(edge.Value)?edge.Value:0),quality.Value/100,watermark,maximumBytes); Message($"Converted {count} images. Originals were kept."); }
            finally { progress.IsActive=false; conversion.Dispose(); conversion=null; }
        },waitOnExit:true));
        Body.Children.Add(AsyncButton("Extract text from images",async ()=>
        {
            if(conversion is not null) { Message("Wait for the current batch or cancel it first.",true); return; }
            if(selected.Count==0 || destination is null) { Message("Choose images and an output folder first.",true); return; }
            conversion=new(); progress.IsActive=true;
            try { int count=await MediaService.Ocr(selected,destination,t=>files.Text=t,conversion.Token); Message($"Saved text from {count} images. English text extraction runs offline."); }
            finally { progress.IsActive=false; conversion.Dispose(); conversion=null; }
        },waitOnExit:true));
        Body.Children.Add(Ui.Button("Cancel",()=>conversion?.Cancel()));
        Body.Children.Add(new HyperlinkButton { Content="Microsoft Visual C++ x64 runtime for text extraction",NavigateUri=new("https://aka.ms/vc14/vc_redist.x64.exe") });
        Body.Children.Add(Ui.Text("New files receive unique names. PNG preserves transparency. JPEG and BMP use an opaque background. Some image formats require a Windows codec.","VelaCaption"));
    }
    private sealed record CommandResult(string Name,Action Action);
}
