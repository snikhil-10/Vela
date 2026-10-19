using System.Text.Json;

namespace Vela.Core;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public bool OnboardingComplete { get; set; }
    public int OnboardingStep { get; set; }
    public HashSet<string> EnabledFeatures { get; set; } = FeatureCatalog.Preset("Everyday");
    public string Theme { get; set; } = "System";
    public bool PinFlyout { get; set; }
    public bool CompactLayout { get; set; }
    public string ShelfDropTarget { get; set; } = "tray";
    public bool ShelfRemoveAfterDrop { get; set; }
    public bool CleaningLockMouse { get; set; }
    public string PanelShortcut { get; set; } = "Ctrl+Alt+V";
    public string CommandShortcut { get; set; } = "Ctrl+Alt+Space";
    public string RadialShortcut { get; set; } = "Ctrl+Alt+R";
    public string PlainPasteShortcut { get; set; } = "Ctrl+Alt+Shift+V";
    public string WindowSearchShortcut { get; set; } = "Ctrl+Alt+W";
    public bool AutoCleanLinks { get; set; }
    public int ClipboardClearSeconds { get; set; } = 60;
    public bool ClearOnLock { get; set; } = true;
    public bool ClearOnSleep { get; set; } = true;
    public bool KeepDisplayAwake { get; set; }
    public bool AwakeOnlyOnAc { get; set; }
    public bool AwakeWithExternalDisplay { get; set; }
    public List<string> AwakeApps { get; set; } = [];
    public List<string> InputExcludedApps { get; set; } = [];
    public List<string> ProtectedApps { get; set; } = [];
    public double ScrollMultiplier { get; set; } = 1;
    public int ClickDebounceMs { get; set; } = 40;
    public string BackButtonShortcut { get; set; } = "";
    public string ForwardButtonShortcut { get; set; } = "";
    public int GpuAlertCelsius { get; set; } = 85;
    public int CpuAlertPercent { get; set; } = 90;
    public int MemoryAlertPercent { get; set; } = 90;
    public int DiskAlertPercent { get; set; } = 10;
    public List<string> FeatureOrder { get; set; } = [];
    public List<Snippet> Snippets { get; set; } = [];
    public List<Launcher> Launchers { get; set; } = [];
    public List<LauncherProfile> LauncherProfiles { get; set; } = [];
    public string ActiveLauncherProfile { get; set; } = "Default";
    public List<WindowLayout> WindowLayouts { get; set; } = [];
    public List<DeveloperFeed> DeveloperFeeds { get; set; } = [];

    public void Normalize()
    {
        EnabledFeatures = (EnabledFeatures ?? []).Where(FeatureCatalog.Contains).ToHashSet();
        FeatureOrder = (FeatureOrder ?? []).Where(FeatureCatalog.Contains).Distinct().ToList();
        DeveloperFeeds = DeveloperFeed.Normalize(DeveloperFeeds);
        Theme = Theme is "Light" or "Dark" ? Theme : "System";
        ShelfDropTarget = ShelfDropTarget is "taskbar" or "velaIcon" or "tray" ? ShelfDropTarget : "manual";
        OnboardingStep = Math.Clamp(OnboardingStep, 0, 3);
        ClipboardClearSeconds = Math.Clamp(ClipboardClearSeconds, 10, 86400);
        ClickDebounceMs = Math.Clamp(ClickDebounceMs, 10, 100);
        ScrollMultiplier = double.IsFinite(ScrollMultiplier) ? Math.Clamp(ScrollMultiplier, .25, 3) : 1;
        GpuAlertCelsius = Math.Clamp(GpuAlertCelsius,60,100);
        CpuAlertPercent = Math.Clamp(CpuAlertPercent, 10, 100);
        MemoryAlertPercent = Math.Clamp(MemoryAlertPercent, 10, 100);
        DiskAlertPercent = Math.Clamp(DiskAlertPercent, 1, 50);
        static List<string> Processes(List<string>? list)=>(list??[]).Where(n=>!string.IsNullOrWhiteSpace(n)).Select(n=>n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        AwakeApps=Processes(AwakeApps); InputExcludedApps=Processes(InputExcludedApps); ProtectedApps=Processes(ProtectedApps);
        Snippets = (Snippets ?? []).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Name)
            && SnippetMatcher.ValidTrigger(s.Trigger) && s.Text is { Length: <= 65536 })
            .Select(s => s with { Expansion = s.Expansion == "afterDelimiter" ? "afterDelimiter" : "immediate" })
            .DistinctBy(s => s.Trigger).Take(512).ToList();
        Launchers = LauncherTree.Normalize(Launchers);
        PanelShortcut = ValidShortcut(PanelShortcut, "Ctrl+Alt+V");
        CommandShortcut = ValidShortcut(CommandShortcut, "Ctrl+Alt+Space");
        RadialShortcut = ValidShortcut(RadialShortcut, "Ctrl+Alt+R");
        PlainPasteShortcut = ValidShortcut(PlainPasteShortcut, "Ctrl+Alt+Shift+V");
        WindowSearchShortcut = ValidShortcut(WindowSearchShortcut, "Ctrl+Alt+W");
        LauncherProfiles=(LauncherProfiles??[]).Where(p=>p is not null && !string.IsNullOrWhiteSpace(p.Name) && !p.Name.Equals("Default",StringComparison.OrdinalIgnoreCase))
            .DistinctBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).Take(24)
            .Select(p=>p with { Items=LauncherTree.Normalize(p.Items) }).ToList();
        if(ActiveLauncherProfile!="Default" && !LauncherProfiles.Any(p=>p.Name==ActiveLauncherProfile)) ActiveLauncherProfile="Default";
        WindowLayouts=(WindowLayouts??[]).Where(l=>l is not null && !string.IsNullOrWhiteSpace(l.Name)).DistinctBy(l=>l.Name,StringComparer.OrdinalIgnoreCase).Take(32)
            .Select(l=>l with { Windows=(l.Windows??[]).Where(w=>w is not null && !string.IsNullOrWhiteSpace(w.Process) && w.Bounds.IsValid).Select(w=>w with { Title=w.Title??"",Monitor=w.Monitor??"" }).Take(80).ToList() }).ToList();
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<Launcher> CurrentLaunchers => ActiveLauncherProfile=="Default" ? Launchers : LauncherProfiles.FirstOrDefault(p=>p.Name==ActiveLauncherProfile)?.Items??Launchers;

    private static string ValidShortcut(string? value, string fallback) => Shortcut.TryParse(value, out _) ? value! : fallback;
}

// Missing Expansion preserves the immediate behavior of older Vela settings.
public sealed record Snippet(string Name, string Trigger, string Text, string Expansion = "immediate");
public sealed record LauncherProfile(string Name,List<Launcher> Items);
public sealed record WindowLayout(string Name,List<SavedWindow> Windows);
public sealed record SavedWindow(string Process,string Title,string Monitor,RelativeBounds Bounds,bool Maximized,long SessionHandle=0,uint SessionProcessId=0);
public readonly record struct RelativeBounds(double X,double Y,double Width,double Height)
{
    public bool IsValid=>double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height)
        && X is >=0 and <=1 && Y is >=0 and <=1 && Width is >0 and <=1 && Height is >0 and <=1;
    public (int X,int Y,int Width,int Height) OnScreen(int left,int top,int width,int height)
    {
        int w=Math.Clamp((int)Math.Round(Width*width),Math.Min(240,width),width);
        int h=Math.Clamp((int)Math.Round(Height*height),Math.Min(160,height),height);
        return (Math.Clamp(left+(int)Math.Round(X*width),left,left+width-w),Math.Clamp(top+(int)Math.Round(Y*height),top,top+height-h),w,h);
    }
}
public sealed record Launcher(string Name, string Target, List<Launcher>? Children = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsFolder => Children is not null;
    public static bool IsAllowedTarget(string? target) => !string.IsNullOrWhiteSpace(target) &&
        (Path.IsPathFullyQualified(target) || Uri.TryCreate(target, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https");
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string Path { get; } = path;
    public string? LoadWarning { get; private set; }
    public AppSettings Load()
    {
        if (!File.Exists(Path)) return new();
        try { return Deserialize(File.ReadAllText(Path)); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LoadWarning = "Your settings could not be read. The original file has been preserved.";
            // Never silently replace the only copy of damaged settings.
            try { File.Copy(Path, Path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), false); }
            catch (Exception backupError) when(backupError is IOException or UnauthorizedAccessException) { }
            return new();
        }
    }
    public static AppSettings Deserialize(string json)
    {
        var result = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? throw new JsonException("Empty settings file.");
        if (result.Version != 1) throw new NotSupportedException("This settings version is not supported.");
        result.Normalize();
        return result;
    }
    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, Serialize(settings));
        File.Move(temp, Path, true);
    }
    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, JsonOptions);
}
