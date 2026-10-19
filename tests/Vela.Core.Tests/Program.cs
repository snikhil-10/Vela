using System.Text.Json;
using Vela.Core;

int passed=0,failed=0;
void Test(string name,Action action)
{
    try { action(); Console.WriteLine("PASS "+name); passed++; }
    catch(Exception ex) { Console.Error.WriteLine("FAIL "+name+": "+ex.Message); failed++; }
}
void Equal<T>(T expected,T actual) { if(!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void True(bool value) { if(!value) throw new Exception("Expected true"); }
void Throws<T>(Action action) where T:Exception { try { action(); } catch(T) { return; } throw new Exception("Expected "+typeof(T).Name); }

Test("Tracking is removed while application query, order, and fragment survive",()=>Equal("https://example.com/a?item=7&sort=a%2Bb#details",LinkCleaner.Clean("https://example.com/a?utm_source=x&item=7&sort=a%2Bb&fbclid=z#details")));
Test("Removing the only tracking key leaves no question mark",()=>Equal("https://example.com/#x",LinkCleaner.Clean("https://example.com/?gclid=a#x")));
Test("Signed AWS links are byte-for-byte unchanged",()=> { var url="https://example.com/?utm_source=required&X-Amz-Signature=a%2Fb"; Equal(url,LinkCleaner.Clean(url)); });
Test("Signed Azure links are byte-for-byte unchanged",()=> { var url="https://example.com/?sig=a%2Fb&utm_source=required"; Equal(url,LinkCleaner.Clean(url)); });
Test("Non-HTTP payloads are never rewritten",()=>Equal("javascript:alert('?utm_source=x')",LinkCleaner.Clean("javascript:alert('?utm_source=x')")));
Test("Text paragraphs are not treated as URLs",()=>Equal("copy https://example.com/?utm_source=x",LinkCleaner.Clean("copy https://example.com/?utm_source=x")));
Test("Encoded tracker names are recognized",()=>Equal("https://example.com/?p=1",LinkCleaner.Clean("https://example.com/?%75tm_source=x&p=1")));
Test("Query-like fragment is never altered",()=>Equal("https://example.com/#?utm_source=x",LinkCleaner.Clean("https://example.com/#?utm_source=x")));
Test("A benign ref parameter is retained",()=>Equal("https://example.com/?ref=product",LinkCleaner.Clean("https://example.com/?ref=product")));
Test("Duplicate application parameters keep their order",()=>Equal("https://example.com/?a=1&a=2",LinkCleaner.Clean("https://example.com/?a=1&utm_medium=x&a=2")));
Test("Unchanged links retain surrounding whitespace",()=>Equal(" https://example.com/a ",LinkCleaner.Clean(" https://example.com/a ")));
Test("Snippet dates are deterministic and locale-independent",()=>Equal("2026-10-14 16:07 2026-10-14 16:07",SnippetExpander.Expand("{date} {time} {datetime}",new(2026,10,14,16,7,0,TimeSpan.FromHours(-7)))));
Test("Clipboard snippet content is not recursively expanded",()=>Equal("{date}",SnippetExpander.Expand("{clipboard}",DateTimeOffset.Now,"{date}")));
Test("Unicode snippet text survives",()=>Equal("こんにちは ✓",SnippetExpander.Expand("こんにちは ✓",DateTimeOffset.Now)));
Test("Unknown snippet variables survive",()=>Equal("{name}",SnippetExpander.Expand("{name}",DateTimeOffset.Now)));
Test("Global shortcut modifiers and key parse",()=> { True(Shortcut.TryParse("Ctrl+Alt+V",out var key)); Equal(3u,key.Modifiers); Equal(86u,key.Key); });
Test("Shortcut parsing is insensitive to spaces and case",()=>True(Shortcut.TryParse(" ctrl + ALT + space ",out _)));
Test("A bare key cannot steal ordinary typing",()=>True(!Shortcut.TryParse("V",out _)));
Test("Duplicate modifiers are rejected",()=>True(!Shortcut.TryParse("Ctrl+Ctrl+V",out _)));
Test("Unknown modifiers are rejected",()=>True(!Shortcut.TryParse("Hyper+V",out _)));
Test("Unsupported function keys are rejected",()=>True(!Shortcut.TryParse("Ctrl+F25",out _)));
Test("The first close press is blocked",()=>True(!new CloseShortcutGuard().Allow("editor",1000,false)));
Test("A held close key cannot confirm",()=> { var guard=new CloseShortcutGuard(); True(!guard.Allow("editor",1000,false)); True(!guard.Allow("editor",1200,true)); });
Test("A distinct second close press is allowed",()=> { var guard=new CloseShortcutGuard(); guard.Allow("editor",1000,false); True(guard.Allow("editor",1800,false)); });
Test("Close confirmation expires",()=> { var guard=new CloseShortcutGuard(); guard.Allow("editor",1000,false); True(!guard.Allow("editor",2400,false)); });
Test("Close actions are isolated",()=> { var guard=new CloseShortcutGuard(); guard.Allow("editor",1000,false); True(!guard.Allow("browser",1200,false)); });
Test("Close confirmation does not permit a third press",()=> { var guard=new CloseShortcutGuard(); guard.Allow("editor",1000,false); guard.Allow("editor",1500,false); True(!guard.Allow("editor",1700,false)); });
Test("An imported native feature cannot become enabled",()=> { var s=SettingsStore.Deserialize("{\"EnabledFeatures\":[\"mixer\",\"switcher\",\"keepAwake\",\"unknownFutureFeature\"]}"); Equal(1,s.EnabledFeatures.Count); True(s.EnabledFeatures.Contains("keepAwake")); });
Test("Imported null collections do not break startup",()=> { var s=SettingsStore.Deserialize("{\"EnabledFeatures\":null,\"Snippets\":null,\"Launchers\":null,\"FeatureOrder\":null}"); Equal(0,s.EnabledFeatures.Count); Equal(0,s.Snippets.Count); });
Test("Null and duplicate process exclusions cannot break input hooks",()=> { var s=SettingsStore.Deserialize("{\"InputExcludedApps\":[null,\"\",\"editor.exe\",\"EDITOR.EXE\"]}"); Equal(1,s.InputExcludedApps.Count); Equal("editor.exe",s.InputExcludedApps[0]); });
Test("Imported bounds and appearance are normalized",()=> { var s=SettingsStore.Deserialize("{\"ClickDebounceMs\":9999,\"ClipboardClearSeconds\":0,\"Theme\":\"Glass\"}"); Equal(100,s.ClickDebounceMs); Equal(10,s.ClipboardClearSeconds); Equal("System",s.Theme); });
Test("Invalid imported shortcuts return to defaults",()=>Equal("Ctrl+Alt+V",SettingsStore.Deserialize("{\"PanelShortcut\":\"V\"}").PanelShortcut));
Test("Invalid and duplicate snippet triggers are removed",()=> { var s=SettingsStore.Deserialize("{\"Snippets\":[{\"Name\":\"A\",\"Trigger\":\";a\",\"Text\":\"one\"},{\"Name\":\"B\",\"Trigger\":\";a\",\"Text\":\"two\"},{\"Name\":\"C\",\"Trigger\":\"bad trigger\",\"Text\":\"bad\"}]}"); Equal(1,s.Snippets.Count); });
Test("Unsafe launcher schemes are rejected",()=> { True(!Launcher.IsAllowedTarget("javascript:alert(1)")); True(!Launcher.IsAllowedTarget("powershell:payload")); True(Launcher.IsAllowedTarget("https://example.com")); });
Test("A future settings schema is not silently downgraded",()=>Throws<NotSupportedException>(()=>SettingsStore.Deserialize("{\"Version\":2}")));
Test("Settings round trip preserves a disabled feature's snippets",()=> { var s=new AppSettings { EnabledFeatures=[],Snippets=[new("A",";a","hello")] }; var restored=SettingsStore.Deserialize(SettingsStore.Serialize(s)); Equal(0,restored.EnabledFeatures.Count); Equal("hello",restored.Snippets[0].Text); });
Test("Settings save is readable and leaves no temporary file",()=>
{
    string directory=Path.Combine(Path.GetTempPath(),"vela-test-"+Guid.NewGuid());
    try { var store=new SettingsStore(Path.Combine(directory,"settings.json")); store.Save(new()); True(store.Load().EnabledFeatures.Contains("keepAwake")); True(!File.Exists(store.Path+".tmp")); }
    finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
});
Test("Malformed settings preserve the original and a recovery copy",()=>
{
    string directory=Path.Combine(Path.GetTempPath(),"vela-test-"+Guid.NewGuid()); Directory.CreateDirectory(directory);
    try { string file=Path.Combine(directory,"settings.json"); File.WriteAllText(file,"broken"); var store=new SettingsStore(file); store.Load(); Equal("broken",File.ReadAllText(file)); True(store.LoadWarning is not null); Equal(2,Directory.GetFiles(directory).Length); }
    finally { Directory.Delete(directory,true); }
});
Test("Disabled resident features are not instantiated",()=> { using var runtime=new FeatureRuntime(); int count=0; runtime.Register("keepAwake",()=> { count++; return new FakeFeature(); }); runtime.Apply([]); Equal(0,count); });
Test("Applying unchanged availability does not restart a service",()=> { using var runtime=new FeatureRuntime(); var fake=new FakeFeature(); runtime.Register("keepAwake",()=>fake); runtime.Apply(["keepAwake"]); runtime.Apply(["keepAwake"]); Equal(1,fake.Starts); });
Test("Disabling a feature tears down its resident service",()=> { using var runtime=new FeatureRuntime(); var fake=new FakeFeature(); runtime.Register("keepAwake",()=>fake); runtime.Apply(["keepAwake"]); runtime.Apply([]); Equal(1,fake.Stops); Equal(1,fake.Disposals); Equal(0,runtime.Running.Count); });
Test("A failed start is reported and cleaned up",()=> { using var runtime=new FeatureRuntime(); var fake=new FakeFeature { FailStart=true }; int failures=0; runtime.Failed+=(_,_)=>failures++; runtime.Register("keepAwake",()=>fake); runtime.Apply(["keepAwake"]); Equal(1,failures); Equal(1,fake.Disposals); Equal(0,runtime.Running.Count); });
Test("One failed stop does not keep another feature running",()=> { using var runtime=new FeatureRuntime(); var a=new FakeFeature { FailStop=true }; var b=new FakeFeature(); runtime.Register("keepAwake",()=>a); runtime.Register("smoothScroll",()=>b); runtime.Apply(["keepAwake","smoothScroll"]); runtime.Apply([]); Equal(1,a.Disposals); Equal(1,b.Stops); Equal(0,runtime.Running.Count); });
Test("Unknown feature factories cannot be started by import",()=> { using var runtime=new FeatureRuntime(); int count=0; runtime.Register("mixer",()=> { count++; return new FakeFeature(); }); runtime.Apply(["mixer"]); Equal(0,count); });
Test("Every preset references only actual implemented tools",()=> { foreach(string preset in new[]{"Everyday","Focused work","Minimal"}) True(FeatureCatalog.Preset(preset).All(FeatureCatalog.Contains)); });
Test("Feature IDs are unique",()=>Equal(FeatureCatalog.All.Count,FeatureCatalog.All.Select(f=>f.Id).Distinct().Count()));
Test("Feature audit covers the 77 pinned upstream features",()=> { using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"feature-audit.json"))); var entries=doc.RootElement.GetProperty("features").EnumerateArray().ToArray(); Equal(77,entries.Length); Equal(77,entries.Select(e=>e.GetProperty("id").GetString()).Distinct().Count()); });
Test("The runtime catalog cannot contain an excluded feature",()=>
{
    using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"feature-audit.json")));
    var excluded=doc.RootElement.GetProperty("features").EnumerateArray().Where(e=>e.GetProperty("decision").GetString()!.StartsWith("exclude")).Select(e=>e.GetProperty("id").GetString()).ToHashSet();
    True(FeatureCatalog.All.All(f=>!excluded.Contains(f.Id)));
});
Test("Workspace positions adapt to a smaller display without leaving its work area",()=>
{ var r=new RelativeBounds(.9,.9,.5,.5).OnScreen(-1920,0,1280,720); Equal(-1280,r.X); Equal(360,r.Y); Equal(640,r.Width); Equal(360,r.Height); });
Test("Workspace minimum size fits even a tiny work area",()=>
{ var r=new RelativeBounds(0,0,.01,.01).OnScreen(0,0,100,80); Equal(100,r.Width); Equal(80,r.Height); });
Test("Invalid workspace coordinates cannot enter settings",()=>
{ var s=new AppSettings { WindowLayouts=[new("Desk",[new("editor","note","display",new(-1,0,.5,.5),false),new("editor","valid","display",new(0,0,.5,.5),false)])] }; s.Normalize(); Equal(1,s.WindowLayouts[0].Windows.Count); });
Test("Launcher profiles preserve the legacy default and expose the chosen profile",()=>
{ var s=new AppSettings { Launchers=[new("Default","https://example.com")],LauncherProfiles=[new("Work",[new("Work app","https://example.org")])],ActiveLauncherProfile="Work" }; var restored=SettingsStore.Deserialize(SettingsStore.Serialize(s)); Equal("Work app",restored.CurrentLaunchers[0].Name); restored.ActiveLauncherProfile="Default"; Equal("Default",restored.CurrentLaunchers[0].Name); });
Test("Invalid active profiles fall back without deleting saved launchers",()=>
{ var s=new AppSettings { ActiveLauncherProfile="Missing",Launchers=[new("Default","https://example.com")] }; s.Normalize(); Equal("Default",s.ActiveLauncherProfile); Equal(1,s.CurrentLaunchers.Count); });
Test("Imported launcher profiles reject unsafe targets",()=>
{ var s=new AppSettings { LauncherProfiles=[new("Work",[new("Unsafe","javascript:alert(1)"),new("Safe","https://example.com")])] }; s.Normalize(); Equal(1,s.LauncherProfiles[0].Items.Count); });
Test("New shortcut settings normalize malformed imports",()=>
{ var s=SettingsStore.Deserialize("{\"PlainPasteShortcut\":\"V\",\"WindowSearchShortcut\":null}"); Equal("Ctrl+Alt+Shift+V",s.PlainPasteShortcut); Equal("Ctrl+Alt+W",s.WindowSearchShortcut); });
Test("Image resize preserves landscape aspect ratio",()=> { var size=ImageSizing.Fit(4000,3000,1000); Equal(1000u,size.Width); Equal(750u,size.Height); });
Test("Image resize preserves portrait aspect ratio",()=> { var size=ImageSizing.Fit(600,1200,400); Equal(200u,size.Width); Equal(400u,size.Height); });
Test("Image resize never upscales or rounds a narrow edge to zero",()=> { Equal((100u,80u),ImageSizing.Fit(100,80,200)); Equal((1u,100u),ImageSizing.Fit(1,20000,100)); });
Test("A zero image edge preserves original dimensions",()=>Equal((640u,480u),ImageSizing.Fit(640,480,0)));
Test("Zero source dimensions are rejected",()=>Throws<ArgumentOutOfRangeException>(()=>ImageSizing.Fit(0,480,200)));
Test("Imported thermal alert thresholds are bounded",()=> { Equal(60,SettingsStore.Deserialize("{\"GpuAlertCelsius\":0}").GpuAlertCelsius); Equal(100,SettingsStore.Deserialize("{\"GpuAlertCelsius\":999}").GpuAlertCelsius); });
Test("Reserved launcher profile names cannot shadow Default",()=> { var s=new AppSettings { LauncherProfiles=[new("default",[new("link","https://example.com")])] }; s.Normalize(); Equal(0,s.LauncherProfiles.Count); });
Test("Duplicate workspace names and null titles normalize safely",()=> { var s=SettingsStore.Deserialize("{\"WindowLayouts\":[{\"Name\":\"Desk\",\"Windows\":[{\"Process\":\"editor\",\"Title\":null,\"Monitor\":null,\"Bounds\":{\"X\":0,\"Y\":0,\"Width\":0.5,\"Height\":0.5}}]},{\"Name\":\"desk\",\"Windows\":[]}]}"); Equal(1,s.WindowLayouts.Count); Equal("",s.WindowLayouts[0].Windows[0].Title); });
foreach (var check in NetworkChecks.All()) Test(check.Name, check.Check);
foreach (var check in LauncherChecks.All()) Test(check.Name, check.Check);
foreach (var check in ImageBudgetChecks.All()) Test(check.Name, check.Check);
InputChecks.Run(Test);
ClipboardChecks.Run(Test);
AlertChecks.Run(Test);
Console.WriteLine($"\n{passed} passed; {failed} failed.");
return failed==0?0:1;

sealed class FakeFeature : IFeatureService
{
    public string Id=>"test";
    public int Starts,Stops,Disposals;
    public bool FailStart,FailStop;
    public void Start() { Starts++; if(FailStart) throw new InvalidOperationException("start failed"); }
    public void Stop() { Stops++; if(FailStop) throw new InvalidOperationException("stop failed"); }
    public void Dispose()=>Disposals++;
}
