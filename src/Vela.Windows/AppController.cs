using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Vela.Core;
using Vela.Windows.Interop;
using Vela.Windows.Services;
using Vela.Windows.UI;

namespace Vela.Windows;

internal sealed class AppController : IDisposable
{
    private readonly SettingsStore store = new(Path.Combine(AppDataPaths.DirectoryPath,"settings.json"));
    private readonly FeatureRuntime runtime=new();
    private readonly List<Window> auxiliary=[];
    private readonly HashSet<Task> operations=[];
    internal ShelfService ShelfData { get; }=new();
    internal System.Collections.ObjectModel.ObservableCollection<ShelfItem> Shelf => ShelfData.Items;
    internal bool ShelfEnabled=>!Quitting && Settings.OnboardingComplete && Settings.EnabledFeatures.Contains("shelf");
    private ShelfDropService? shelfDrops;
    private ThermalAlerts? thermalAlerts;
    private ResourceAlerts? resourceAlerts;
    private ToolWindow? shelfWindow;
    private bool keepShelfIcon, closingShelf;
    public AppSettings Settings { get; private set; }
    public MainWindow Panel { get; private set; }=null!;
    public AwakeService Awake { get; private set; }=null!;
    public DisplayDimmer Dimmer { get; private set; }=null!;
    public InputService Input { get; private set; }=null!;
    public WindowWorkspace Workspaces { get; }=new();
    internal ImagePins Pins { get; private set; }=null!;
    public string? LastError { get; private set; }
    private WindowMessages messages=null!;
    private TrayIcon tray=null!;
    private ShortcutService shortcuts=null!;
    private ClipboardService clipboard=null!;
    private SettingsWindow? settingsWindow;
    private ToolWindow? tools;
    private RadialWindow? radial;
    public bool Quitting { get; private set; }
    private bool disposed;
    public event Action? Changed;
    public AppController() { Settings=store.Load(); }
    public void Initialize(bool startup)
    {
        Panel=new(this);
        Pins=new(Track,()=>!Quitting && Settings.OnboardingComplete && Settings.EnabledFeatures.Contains("imagePins"));
        var hwnd=WindowStyle.Handle(Panel); var queue=DispatcherQueue.GetForCurrentThread();
        messages=new(hwnd);
        messages.Error+=Report;
        tray=new(hwnd,messages); tray.Click+=TogglePanel; tray.Settings+=ShowSettings;
        tray.ThemeChanged+=()=>
        {
            if(Quitting) return;
            WindowIcons.Refresh(Panel);
            foreach(var window in auxiliary.ToArray()) WindowIcons.Refresh(window);
        };
        tray.Quit+=()=>((App)Application.Current).Quit();
        Awake=new(()=>Settings,queue); Dimmer=new(); Input=new(()=>Settings,queue,RunOperation,hwnd);
        clipboard=new(hwnd,messages,()=>Settings,queue); clipboard.Error+=Report;
        clipboard.PasteFailed+=text=> { Report(text); tray.Notify("Plain paste",text); };
        shortcuts=new(hwnd,messages,()=>Settings,TogglePanel,()=>OpenTool("commandBar"),ShowRadial,()=> { _=RunOperation(clipboard.PastePlain); },()=>OpenTool("windowSearch"));
        shortcuts.Error+=Report; Input.Notice+=text=>tray.Notify("Vela",text);
        Awake.Changed+=()=>Panel.RefreshAwake();
        Awake.Error+=Report;
        runtime.Register("shelf",()=>shelfDrops=new(this,queue,()=>tray.Bounds));
        runtime.Register("windowSearch",()=>new DelegateFeature("windowSearch",()=>{},Workspaces.UnpinAll));
        runtime.Register("imagePins",()=>new DelegateFeature("imagePins",()=>{},Pins.CloseAll));
        runtime.Register("keepAwake",()=>Awake);
        runtime.Register("displayDimmer",()=>Dimmer);
        foreach(var id in new[]{"textSnippets","smoothScroll","scrollHorizontal","scrollInverter","mouseButtonShortcuts","mouseClickDebounce","superKey","quitWindowProtection","cleaningMode"})
        { string captured=id; runtime.Register(id,()=>Input.Feature(captured)); }
        runtime.Register("urlCleaner",()=>clipboard.Feature("urlCleaner"));
        runtime.Register("clipboardGuard",()=>clipboard.Feature("clipboardGuard"));
        runtime.Register("thermalAlerts",()=>thermalAlerts=new(()=>Settings,queue,tray.Notify));
        runtime.Register("resourceAlerts",()=>resourceAlerts=new(()=>Settings,queue,tray.Notify));
        runtime.Failed+=(id,error)=>Report($"{FeatureCatalog.Get(id).Name} could not start: {error}");
        messages.Message+=(msg,wp,lp)=>
        {
            if(msg==0x7E) Dimmer.Stop();
            if(msg==Native.WM_WTSSESSION_CHANGE && wp==7 || msg==Native.WM_POWERBROADCAST && wp==4)
            { ShelfData.CancelPendingAdds(); clipboard.CancelPaste(); Input.SuspendInput(); thermalAlerts?.ResetObservations(); resourceAlerts?.ResetObservations(); }
            if(!Quitting && (msg==Native.WM_WTSSESSION_CHANGE && wp==8 || msg==Native.WM_POWERBROADCAST && wp is 7 or 18))
            { thermalAlerts?.ResetObservations(); resourceAlerts?.ResetObservations(); Input.ResumeInput(); }
        };
        Panel.Refresh();
        if(Settings.OnboardingComplete) { Apply(); if(!startup) ShowPanel(); }
        else ShowOnboarding();
        if(store.LoadWarning is {} warning) Report(warning);
    }
    public void Save(bool apply=true)
    {
        try { store.Save(Settings); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Report("Settings could not be saved: "+ex.Message); }
        SettingsChanged(apply);
    }
    private void SettingsChanged(bool apply)
    {
        clipboard?.OptionsChanged(); Input?.OptionsChanged();
        if(apply) Apply();
        shelfDrops?.OptionsChanged();
        foreach(var window in auxiliary) WindowStyle.ApplyTheme(window,Settings.Theme);
        WindowStyle.ApplyTheme(Panel,Settings.Theme);
        Changed?.Invoke(); Panel.Refresh();
    }
    private void Apply()
    {
        if(!Settings.OnboardingComplete || !Settings.EnabledFeatures.Contains("shelf")) ShelfData.CancelPendingAdds();
        runtime.Apply(Settings.OnboardingComplete?Settings.EnabledFeatures:[]);
        shortcuts.Update(Settings.OnboardingComplete);
        if(!Settings.EnabledFeatures.Contains("keepAwake")) Awake.Release();
        if(tools is not null && !Settings.EnabledFeatures.Contains(tools.FeatureId)) tools.Close();
        if(!Settings.EnabledFeatures.Contains("radialMenu")) radial?.Close();
        if(!Settings.EnabledFeatures.Contains("shelf") && shelfWindow is not null) { closingShelf=true; shelfWindow.Close(); closingShelf=false; }
    }
    public void SetFeature(string id,bool enabled)
    {
        if(!FeatureCatalog.Contains(id)) return;
        if(enabled) Settings.EnabledFeatures.Add(id); else Settings.EnabledFeatures.Remove(id);
        Save();
    }
    public void ShowPanel()
    {
        if(Quitting) return;
        if(!Settings.OnboardingComplete) { ShowOnboarding(); return; }
        Panel.Refresh();
        Native.ShowWindow(WindowStyle.Handle(Panel),9); // SW_RESTORE also undoes Win+D/minimized state.
        WindowStyle.NearPointer(Panel,420,Settings.CompactLayout?500:620,tray.Bounds);
        Panel.Activate(); Native.SetForegroundWindow(WindowStyle.Handle(Panel));
    }
    private void TogglePanel()
    { if(Panel.AppWindow.IsVisible && !Native.IsIconic(WindowStyle.Handle(Panel))) Panel.AppWindow.Hide(); else ShowPanel(); }
    public void ShowSettings()
    {
        if(Quitting) return;
        Panel.AppWindow.Hide();
        if(settingsWindow is null) { settingsWindow=new(this); Track(settingsWindow); settingsWindow.Closed+=(_,_)=>settingsWindow=null; }
        settingsWindow.Activate();
    }
    public void ShowShortcutSettings() { ShowSettings(); settingsWindow!.Navigate("Shortcuts"); }
    public void OpenTool(string id)
    {
        if(Quitting || !Settings.OnboardingComplete || !Settings.EnabledFeatures.Contains(id)) return;
        Panel.AppWindow.Hide();
        if(id=="quickLauncher") { ShowPanel(); return; }
        if(id=="shelf") { ShowShelf(); return; }
        if(id=="radialMenu") { ShowRadial(); return; }
        if(FeatureCatalog.Get(id).Group=="Input" && id!="textSnippets") { ShowSettings(); settingsWindow!.Navigate("Input"); return; }
        ShowToolWindow(id);
    }
    private ToolWindow CreateShelfWindow()
    {
        var window=new ToolWindow(this,"shelf"); shelfWindow=window; window.Title="Vela · File shelf"; Track(window);
        window.AppWindow.Closing+=(_,e)=>
        {
            if(keepShelfIcon && !closingShelf && !Quitting) { e.Cancel=true; Native.ShowWindow(WindowStyle.Handle(window),7); }
        };
        window.Closed+=(_,_)=> { if(ReferenceEquals(shelfWindow,window)) shelfWindow=null; };
        return window;
    }
    internal void ShowShelf()
    {
        if(!ShelfEnabled) return;
        Panel.AppWindow.Hide(); var window=shelfWindow??CreateShelfWindow();
        Native.ShowWindow(WindowStyle.Handle(window),9); window.Activate(); Native.SetForegroundWindow(WindowStyle.Handle(window));
    }
    internal void KeepShelfTaskbarIcon(bool keep)
    {
        keepShelfIcon=keep && ShelfEnabled;
        if(keepShelfIcon && shelfWindow is null)
        { var window=CreateShelfWindow(); Native.ShowWindow(WindowStyle.Handle(window),7); }
        else if(!keepShelfIcon && shelfWindow is not null && Native.IsIconic(WindowStyle.Handle(shelfWindow)))
        { closingShelf=true; shelfWindow.Close(); closingShelf=false; }
    }
    public void EditLaunchers()
    {
        if(Quitting || !Settings.OnboardingComplete || !Settings.EnabledFeatures.Contains("radialMenu")) return;
        Panel.AppWindow.Hide(); ShowToolWindow("radialMenu");
    }
    private void ShowToolWindow(string id)
    {
        tools?.Close();
        var window=new ToolWindow(this,id); tools=window; Track(window);
        window.Closed+=(_,_)=> { if(ReferenceEquals(tools,window)) tools=null; };
        window.Activate(); Native.SetForegroundWindow(WindowStyle.Handle(window));
    }
    public void ShowRadial()
    {
        if(Quitting || !Settings.OnboardingComplete || !Settings.EnabledFeatures.Contains("radialMenu")) return;
        if(radial is not null) { radial.Activate(); Native.SetForegroundWindow(WindowStyle.Handle(radial)); return; }
        var window=new RadialWindow(this); radial=window; Track(window);
        window.Closed+=(_,_)=> { if(ReferenceEquals(radial,window)) radial=null; };
        WindowStyle.NearPointer(window,RadialWindow.WidthDips,RadialWindow.HeightDips); window.Activate();
        Native.SetForegroundWindow(WindowStyle.Handle(window));
    }
    private OnboardingWindow? onboarding;
    public void ShowOnboarding(bool restart=false)
    {
        if(Quitting) return;
        if(restart && onboarding is null) Settings.OnboardingStep=0;
        if(onboarding is null) { onboarding=new(this); Track(onboarding); onboarding.Closed+=(_,_)=>onboarding=null; }
        onboarding.Activate();
    }
    public void CompleteOnboarding()
    { Settings.OnboardingComplete=true; Settings.OnboardingStep=3; Save(); ShowPanel(); }
    public void Import(AppSettings imported)
    {
        // Keep the current session intact if persistence fails. Work on an
        // independent normalized copy rather than altering the reviewed backup.
        var replacement=SettingsStore.Deserialize(SettingsStore.Serialize(imported));
        replacement.OnboardingComplete=Settings.OnboardingComplete; replacement.OnboardingStep=Settings.OnboardingStep;
        store.Save(replacement);
        Settings=replacement; SettingsChanged(true);
    }
    public void Track(Window window)
    { auxiliary.Add(window); window.Closed+=(_,_)=>auxiliary.Remove(window); WindowStyle.ApplyTheme(window,Settings.Theme); }
    public void Report(string message) { LastError=message; Panel.ShowError(message); }
    internal async Task RunOperation(Func<Task> action)
    {
        if(Quitting) return;
        var operation=action(); operations.Add(operation);
        try { await operation; } finally { operations.Remove(operation); }
    }
    internal async Task FinishOperations()
    {
        ShelfData.CancelPendingAdds();
        Quitting=true; clipboard?.CancelPaste(); Input?.SuspendInput();
        foreach(var window in auxiliary.ToArray())
        {
            if(window is ToolWindow tool) tool.CancelWork();
            if(window is SettingsWindow settings) settings.CancelWork();
            window.AppWindow.Hide();
        }
        Panel?.AppWindow.Hide();
        // Keep the dispatcher alive until cancellation has removed incomplete output files.
        // Closed tool windows may still have work finishing, so track operations independently of windows.
        try { await Task.WhenAll(operations.ToArray()); }
        catch(OperationCanceledException) { /* Expected when canceled picker/review work is drained on Exit. */ }
        catch(Exception error) { StartupDiagnostics.Record("Finishing canceled work",error); }
    }
    public IEnumerable<FeatureDefinition> OrderedFeatures => FeatureCatalog.All.OrderBy(f=>
    { int i=Settings.FeatureOrder.IndexOf(f.Id); return i<0?1000+FeatureCatalog.All.ToList().IndexOf(f):i; });
    public void Dispose()
    {
        if(disposed) return;
        disposed=true;
        ShelfData.CancelPendingAdds();
        Quitting=true; runtime.Dispose(); Workspaces.Dispose(); Input?.Dispose(); clipboard?.Dispose(); Dimmer?.Dispose(); shortcuts?.Dispose(); tray?.Dispose(); messages?.Dispose();
        foreach(var window in auxiliary.ToArray()) window.Close(); Panel?.Close();
    }
}
