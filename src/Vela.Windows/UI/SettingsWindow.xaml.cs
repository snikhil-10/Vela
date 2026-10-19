using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Vela.Core;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class SettingsWindow : Window
{
    private readonly AppController controller;
    private Action? reflowFeatures;
    internal SettingsWindow(AppController controller)
    {
        this.controller=controller; InitializeComponent(); WindowStyle.Configure(this,1060,800);
        Rows.SizeChanged+=(_,_)=>reflowFeatures?.Invoke();
        Navigation.SelectedItem=Navigation.MenuItems[0];
    }
    public void Navigate(string name)
    { Navigation.SelectedItem=Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i=>(string)i.Tag==name)??Navigation.MenuItems[0]; }
    private void NavigationChanged(NavigationView sender,NavigationViewSelectionChangedEventArgs e)
    {
        if(e.SelectedItemContainer?.Tag is not string page) return;
        reflowFeatures=null; Rows.Children.Clear(); Heading.Text=page=="Data"?"Access & data":page; Status.IsOpen=false;
        switch(page) { case "General": General(); break; case "Features": Features(); break; case "Input": Input(); break; case "Shortcuts": Shortcuts(); break; case "Data": Data(); break; case "About": About(); break; }
    }
    private void Add(string name,string description,UIElement? control=null)=>Rows.Children.Add(Ui.Card(name,description,control));
    private void Message(string text,bool error=false) { Status.Message=text; Status.Severity=error?InfoBarSeverity.Error:InfoBarSeverity.Success; Status.IsOpen=true; }
    private void General()
    {
        var theme=new ComboBox { ItemsSource=new[]{"System","Light","Dark"},SelectedItem=controller.Settings.Theme,Width=140 };
        theme.SelectionChanged+=(_,_)=> { controller.Settings.Theme=(string)theme.SelectedItem; controller.Save(); };
        Add("Appearance","Follow Windows or choose your preferred appearance.",theme);
        Add("Start at sign-in","Open quietly in the system tray when you sign in.",Ui.Toggle(StartupService.Enabled,on=>
        { try { StartupService.Enabled=on; } catch(Exception ex) { Message(ex.Message,true); } }));
        Add("Compact panel","Use a shorter panel with a scrollable tool grid.",Ui.Toggle(controller.Settings.CompactLayout,on=> { controller.Settings.CompactLayout=on; controller.Save(false); }));
        Add("Keep panel open","Keep your tools visible when you click another window.",Ui.Toggle(controller.Settings.PinFlyout,on=> { controller.Settings.PinFlyout=on; controller.Save(false); }));
        Add("Make it yours","Features can be added and removed at any time.",Ui.Button("Choose features",()=>Navigate("Features")));
        Add("Welcome experience","Review the introduction and access explanations.",Ui.Button("Open welcome",()=>controller.ShowOnboarding(true)));
        Add("Exit Vela","Release keep-awake requests, remove dimming, and stop all input hooks.",Ui.Button("Exit",()=>((App)Application.Current).Quit()));
    }
    private void Features()
    {
        Rows.Children.Add(Ui.Text("Choose the extras that earn their place in your tray. Disabled features stop listening and keep their saved settings.","VelaBody"));
        var search=new TextBox { PlaceholderText="Find a feature" }; Rows.Children.Add(search);
        var groups=Ui.Stack(16); Rows.Children.Add(groups);
        int columns=Rows.ActualWidth>=620?2:1;
        void Render()
        {
            groups.Children.Clear();
            foreach(var group in controller.OrderedFeatures.Where(f=>(f.Name+" "+f.Description).Contains(search.Text,StringComparison.OrdinalIgnoreCase)).GroupBy(f=>f.Group))
            {
                groups.Children.Add(Ui.Text(group.Key,"SubtitleTextBlockStyle"));
                var grid=new Grid { ColumnSpacing=8,RowSpacing=8 };
                for(int c=0;c<columns;c++) grid.ColumnDefinitions.Add(new() { Width=new(1,GridUnitType.Star) });
                int index=0;
                foreach(var feature in group)
                {
                    var stack=Ui.Stack(12);
                    var icon=new FontIcon { Glyph=feature.Glyph,FontSize=24,HorizontalAlignment=HorizontalAlignment.Left };
                    var toggle=Ui.Toggle(controller.Settings.EnabledFeatures.Contains(feature.Id),on=>controller.SetFeature(feature.Id,on));
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,feature.Name);
                    stack.Children.Add(icon); stack.Children.Add(Ui.Text(feature.Name,"BodyStrongTextBlockStyle"));
                    stack.Children.Add(Ui.Text(feature.Description,"VelaCaption")); stack.Children.Add(toggle);
                    var card=new Border { Style=Ui.Style("VelaCard"),Child=stack };
                    int col=index%columns,row=index/columns;
                    if(col==0) grid.RowDefinitions.Add(new() { Height=GridLength.Auto });
                    Grid.SetRow(card,row); Grid.SetColumn(card,col); grid.Children.Add(card); index++;
                }
                groups.Children.Add(grid);
            }
        }
        reflowFeatures=()=> { int next=Rows.ActualWidth>=620?2:1; if(next!=columns) { columns=next; Render(); } };
        search.TextChanged+=(_,_)=>Render(); Render();
        Rows.Children.Add(Ui.Text("Panel order","SubtitleTextBlockStyle"));
        var order=Ui.Stack(6); Rows.Children.Add(order);
        void RenderOrder()
        {
            order.Children.Clear(); var features=controller.OrderedFeatures.ToList();
            for(int i=0;i<features.Count;i++)
            {
                var feature=features[i]; int position=i;
                var buttons=new StackPanel { Orientation=Orientation.Horizontal,Spacing=4 };
                void Move(int delta)
                {
                    int target=position+delta; if(target<0 || target>=features.Count) return;
                    (features[position],features[target])=(features[target],features[position]);
                    controller.Settings.FeatureOrder=features.Select(f=>f.Id).ToList(); controller.Save(false); RenderOrder();
                }
                var up=Ui.Button("↑",()=>Move(-1)); up.IsEnabled=i>0;
                var down=Ui.Button("↓",()=>Move(1)); down.IsEnabled=i<features.Count-1;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(up,"Move "+feature.Name+" up");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(down,"Move "+feature.Name+" down");
                buttons.Children.Add(up); buttons.Children.Add(down); order.Children.Add(Ui.Card(feature.Name,"",buttons));
            }
        }
        RenderOrder();
    }
    private void Input()
    {
        Rows.Children.Add(Ui.Text("Input tools run only while enabled. They work in regular desktop apps; Windows prevents them from controlling elevated apps and secure screens.","VelaBody"));
        Add("Smooth scrolling speed","Applied to traditional mouse wheels. High-resolution wheel events pass through.",Ui.Number(controller.Settings.ScrollMultiplier*100,25,300,n=> { controller.Settings.ScrollMultiplier=n/100.0; controller.Save(false); }));
        Add("Extra click filter","Ignore duplicate button presses within this many milliseconds. Start with 40 ms.",Ui.Number(controller.Settings.ClickDebounceMs,10,100,n=> { controller.Settings.ClickDebounceMs=n; controller.Save(false); }));
        TextSetting("Back button shortcut","Leave empty to keep native Back behavior.",controller.Settings.BackButtonShortcut,value=>controller.Settings.BackButtonShortcut=value,true);
        TextSetting("Forward button shortcut","Leave empty to keep native Forward behavior.",controller.Settings.ForwardButtonShortcut,value=>controller.Settings.ForwardButtonShortcut=value,true);
        ProcessList("Apps to leave alone","One process name per line, such as game.exe. Applies to input enhancements.",controller.Settings.InputExcludedApps,value=>controller.Settings.InputExcludedApps=value);
        ProcessList("Close protection","Only these apps require a second Alt+F4 or Ctrl+W press. One process name per line.",controller.Settings.ProtectedApps,value=>controller.Settings.ProtectedApps=value);
        Add("Super key","Hold Caps Lock for Ctrl + Alt + Shift + Windows. Tap for Escape. Disable the feature to restore normal Caps Lock.");
        Add("Text snippets","Automatic expansion is intended for ordinary text fields. IME composition, elevated apps, and some custom editors are not supported.",Ui.Button("Manage snippets",()=>controller.OpenTool("textSnippets")));
    }
    private void TextSetting(string title,string description,string value,Action<string> set,bool optional=false)
    {
        var stack=Ui.Stack(8); stack.Children.Add(Ui.Text(title,"BodyStrongTextBlockStyle")); stack.Children.Add(Ui.Text(description,"VelaCaption"));
        var box=new TextBox { Text=value,PlaceholderText="Ctrl+Alt+V" }; stack.Children.Add(box);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box,title);
        stack.Children.Add(Ui.Button("Save",()=>
        {
            if(!(optional && string.IsNullOrWhiteSpace(box.Text)) && !Shortcut.TryParse(box.Text,out _)) { Message("Use a modifier and a key, for example Ctrl+Alt+V.",true); return; }
            set(box.Text.Trim()); controller.Save(); Message("Saved. Any shortcut conflict will also appear in the tray panel.");
        })); Rows.Children.Add(new Border { Style=Ui.Style("VelaCard"),Child=stack });
    }
    private void ProcessList(string title,string description,List<string> current,Action<List<string>> set)
    {
        var stack=Ui.Stack(8); stack.Children.Add(Ui.Text(title,"BodyStrongTextBlockStyle")); stack.Children.Add(Ui.Text(description,"VelaCaption"));
        var box=new TextBox { Text=string.Join(Environment.NewLine,current),AcceptsReturn=true,MinHeight=76,TextWrapping=TextWrapping.Wrap };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box,title);
        stack.Children.Add(box); stack.Children.Add(Ui.Button("Save",()=> { set(box.Text.Split(['\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList()); controller.Save(false); Message("Saved."); }));
        Rows.Children.Add(new Border { Style=Ui.Style("VelaCard"),Child=stack });
    }
    private void Shortcuts()
    {
        TextSetting("Quick panel","Open your tools from any regular desktop app.",controller.Settings.PanelShortcut,v=>controller.Settings.PanelShortcut=v);
        TextSetting("Command bar","Search enabled tools, snippets, and saved launchers.",controller.Settings.CommandShortcut,v=>controller.Settings.CommandShortcut=v);
        TextSetting("Radial menu","Open saved launchers near the pointer.",controller.Settings.RadialShortcut,v=>controller.Settings.RadialShortcut=v);
        TextSetting("Plain paste","Paste in the focused app, then restore the original clipboard. Regular desktop apps only.",controller.Settings.PlainPasteShortcut,v=>controller.Settings.PlainPasteShortcut=v);
        TextSetting("Find a window","Search open titles and filter by display.",controller.Settings.WindowSearchShortcut,v=>controller.Settings.WindowSearchShortcut=v);
        Add("Scroll sideways","Hold Alt while scrolling when Scroll sideways is enabled.");
        Add("Native Windows shortcuts","Windows shortcuts remain available: Win+V for clipboard history, Win+Z for Snap, Win+Shift+S for screenshots, and Win+Shift+R for recordings.");
    }
    private void Data()
    {
        Add("Stays on this PC","Vela has no account, analytics, cloud sync, or automatic network requests. Settings, snippets, and launcher paths are stored in your local Windows profile.");
        Add("Optional connection test","Internet speed test contacts Cloudflare only when you start it. A run uses up to 80 MB plus overhead. Cloudflare receives your public IP and generated test traffic; Vela sends no user content or results report.");
        foreach(var feature in FeatureCatalog.All.Where(f=>controller.Settings.EnabledFeatures.Contains(f.Id))) Add(feature.Name,feature.Access);
        Add("Clipboard history","Auto-clear clears the current clipboard. Pinned and saved Windows clipboard history is owned by Windows; manage it in Windows Settings.");
        Add("Export settings","Includes snippets and launcher paths. Keep the exported file somewhere private.",Ui.Button("Export",Export));
        Add("Import settings","Review and apply a Vela settings file. Sign-in startup is managed separately.",Ui.Button("Import",Import));
        if(controller.LastError is {} error) Add("Last notice",error);
    }
    private async void Export()
    {
        try
        {
            var picker=new FileSavePicker { SuggestedFileName="Vela settings" }; picker.FileTypeChoices.Add("Vela settings",[".json"]);
            InitializeWithWindow.Initialize(picker,WindowStyle.Handle(this)); var file=await picker.PickSaveFileAsync();
            if(file is null) return; await FileIO.WriteTextAsync(file,SettingsStore.Serialize(controller.Settings)); Message("Settings exported.");
        }
        catch(Exception ex) { Message(ex.Message,true); }
    }
    private async void Import()
    {
        try
        {
            var picker=new FileOpenPicker(); picker.FileTypeFilter.Add(".json"); InitializeWithWindow.Initialize(picker,WindowStyle.Handle(this));
            var file=await picker.PickSingleFileAsync(); if(file is null) return;
            if((await file.GetBasicPropertiesAsync()).Size>4*1024*1024) { Message("That file is too large for a settings backup.",true); return; }
            var imported=SettingsStore.Deserialize(await FileIO.ReadTextAsync(file));
            var dialog=new ContentDialog { XamlRoot=Root.XamlRoot,Title="Replace your Vela settings?",Content=$"This will enable {imported.EnabledFeatures.Count} tools and import {imported.Snippets.Count} snippets. Existing settings will be replaced.",PrimaryButtonText="Import",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close };
            if(await dialog.ShowAsync()==ContentDialogResult.Primary) { controller.Import(imported); Message("Settings imported."); }
        }
        catch(Exception ex) { Message(ex.Message,true); }
    }
    private void About()
    {
        Add("Vela · 0.2.2","An independent Windows port inspired by Vorssaint. Native WinUI 3, XAML, and C#. GPL-3.0-or-later. Not affiliated with or endorsed by the Vorssaint maintainer.");
        Add("Made for Windows 11","Mica materials, Segoe UI Variable, native controls, system theme, and accessible keyboard navigation. Windows-native features are excluded from the tool catalog.");
        var link=new HyperlinkButton { Content="Vorssaint source and credits",NavigateUri=new("https://github.com/vorssaint/vorssaint-utils") };
        Rows.Children.Add(link);
    }
}
