using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vela.Core;
using Vela.Windows;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;

internal static class Program
{
    private const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static readonly List<string> results=[];
    private static readonly string log=Path.Combine(AppContext.BaseDirectory,"runtime-lifecycle.log");
    private static readonly string profile=Path.Combine(AppContext.BaseDirectory,"profile");
    [STAThread]
    private static void Main()
    {
        try
        {
            Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile,"settings.json"),"{\"Version\":1,\"OnboardingComplete\":true,\"EnabledFeatures\":[],\"ShelfDropTarget\":\"manual\"}");
            typeof(App).Assembly.GetType("Vela.Windows.AppDataPaths")!.GetMethod("Configure",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[new string[]{"--data-directory",profile}]);
            results.Add("INFO Exact candidate DLL SHA256="+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(App).Assembly.Location))).ToLowerInvariant());
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_=>
            {
                var queue=DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
                var app=new FixtureApp();
                queue.TryEnqueue(async ()=>
                {
                    Window? anchor=null;
                    try
                    {
                        anchor=new Window { Content=new Grid() }; anchor.Activate(); anchor.AppWindow.Hide();
                        object uninitialized=Create("Vela.Windows.AppController");
                        await Async(uninitialized,"FinishOperations");
                        ((IDisposable)uninitialized).Dispose(); ((IDisposable)uninitialized).Dispose();
                        results.Add("PASS controller cleanup before initialization and repeated disposal.");
                        await AlertServiceLifecycle();
                        DisabledAwakeCannotRestart();
                        await AwakeLifecycle();
                        await OnboardingGates(); await SettingsImportTransaction(); await SettingsTransferLifecycle(); await FaultedExitDiagnostics();
                        await WheelDelivery(); await ControllerLifecycle(); results.Add("PASS native runtime component checks.");
                    }
                    catch(Exception error) { results.Add("FAIL "+error); Environment.ExitCode=1; }
                    finally { anchor?.Close(); File.WriteAllLines(log,results); typeof(App).GetMethod("Quit",Members)!.Invoke(app,null); }
                });
            });
        }
        catch(Exception error) { File.WriteAllText(log,"FAIL "+error); Environment.ExitCode=1; }
    }
    private static object Create(string type,params object?[] args)=>Activator.CreateInstance(typeof(App).Assembly.GetType(type)!,Members,null,args,null)!;
    private static object? Get(object instance,string name)=>instance.GetType().GetProperty(name,Members)!.GetValue(instance);
    private static object? Field(object instance,string name)=>instance.GetType().GetField(name,Members)!.GetValue(instance);
    private static void SetField(object instance,string name,object? value)=>instance.GetType().GetField(name,Members)!.SetValue(instance,value);
    private static object? Call(object instance,string method,params object?[] args)=>instance.GetType().GetMethod(method,Members)!.Invoke(instance,args);
    private static Task Async(object instance,string method,params object?[] args)=>(Task)Call(instance,method,args)!;
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    private static void Step(string value) { results.Add("STEP "+value); File.WriteAllLines(log,results); }
    private static SoftwareBitmap Bitmap()
    {
        var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,32,16,BitmapAlphaMode.Premultiplied);
        byte[] pixels=new byte[32*16*4]; for(int index=0;index<pixels.Length;index+=4) { pixels[index]=24; pixels[index+1]=48; pixels[index+2]=96; pixels[index+3]=255; }
        bitmap.CopyFromBuffer(pixels.AsBuffer()); return bitmap;
    }
    private static void DisabledAwakeCannotRestart()
    {
        Step("A stale Keep awake action cannot restart a stopped service");
        object awake=Create("Vela.Windows.Services.AwakeService",(Func<AppSettings>)(()=>new AppSettings { EnabledFeatures=[] }),DispatcherQueue.GetForCurrentThread());
        try
        {
            Call(awake,"Stop"); Call(awake,"Hold",TimeSpan.FromMinutes(1));
            Require(!(bool)Get(awake,"IsAwake")!,"A stopped Keep awake service accepted a stale hold action.");
            results.Add("PASS stopped Keep awake ignores a stale hold and owns no wake request.");
        }
        finally { Call(awake,"Stop"); ((IDisposable)awake).Dispose(); }
    }
    private static async Task AlertServiceLifecycle()
    {
        Step("Stopped alert services reject queued callbacks and repeated Start owns one timer");
        var notifications=new List<string>();
        object resource=Create("Vela.Windows.Services.ResourceAlerts",(Func<AppSettings>)(()=>new AppSettings()),DispatcherQueue.GetForCurrentThread(),(Action<string,string>)((title,_)=>notifications.Add(title)));
        try
        {
            Call(resource,"Start"); var timer=(DispatcherQueueTimer)Field(resource,"timer")!;
            Call(resource,"Stop"); Require(!timer.IsRunning && !(bool)Field(resource,"cpuBaseline")!,"Resource stop retained its timer/baseline.");
            Call(resource,"Tick",timer,new object());
            Require(!(bool)Field(resource,"cpuBaseline")! && notifications.Count==0,"A queued resource callback sampled or notified after Stop.");
            Call(resource,"Start"); var current=(DispatcherQueueTimer)Field(resource,"timer")!;
            Call(resource,"Start"); Require(ReferenceEquals(current,Field(resource,"timer")),"Resource Start duplicated its timer.");
            Call(resource,"Stop"); Require(!current.IsRunning,"Resource stop left its original timer active.");
        }
        finally { ((IDisposable)resource).Dispose(); }
        object thermal=Create("Vela.Windows.Services.ThermalAlerts",(Func<AppSettings>)(()=>new AppSettings()),DispatcherQueue.GetForCurrentThread(),(Action<string,string>)((title,_)=>notifications.Add(title)));
        try
        {
            Call(thermal,"Start"); var timer=(DispatcherQueueTimer)Field(thermal,"timer")!;
            Call(thermal,"Start"); Require(ReferenceEquals(timer,Field(thermal,"timer")),"Thermal Start duplicated its timer.");
            Call(thermal,"Tick",timer,new object()); Call(thermal,"Stop");
            var deadline=System.Diagnostics.Stopwatch.StartNew();
            while((bool)Field(thermal,"busy")! && deadline.Elapsed<TimeSpan.FromSeconds(10)) await Task.Delay(25);
            Require(!(bool)Field(thermal,"busy")! && !(bool)Field(thermal,"running")! && !timer.IsRunning && notifications.Count==0,"Thermal stop left work, a timer or a late notification.");
            Call(thermal,"Tick",timer,new object()); Require(!(bool)Field(thermal,"busy")!,"A stale thermal callback sampled after Stop.");
        }
        finally { ((IDisposable)thermal).Dispose(); }
        results.Add("PASS resource and thermal lifecycle: one timer per active service, Stop detaches/stops it, stale callbacks do no work, in-flight read-only thermal sampling drains without late notification. Notifications are captured, not displayed; GPU threshold/hot-plug hardware is not claimed.");
    }
    private static async Task AwakeLifecycle()
    {
        Step("Production Keep awake timer, power/rules and native request lifecycle");
        var settings=new AppSettings { EnabledFeatures=[],AwakeOnlyOnAc=true };
        var clock=new FixtureClock(); bool? ac=true; bool display=false; string[] processes=[]; int probes=0;
        bool failAc=false,failDisplay=false,failProcesses=false,refuseRequest=false; var errors=new List<string>();
        var writes=new List<(bool System,bool Display)>();
        object awake=Create("Vela.Windows.Services.AwakeService",(Func<AppSettings>)(()=>settings),DispatcherQueue.GetForCurrentThread(),clock,
            (Func<bool?>)(()=> { probes++; if(failAc) throw new InvalidOperationException("Generated unavailable AC probe"); return ac; }),
            (Func<bool>)(()=> { probes++; if(failDisplay) throw new InvalidOperationException("Generated unavailable display probe"); return display; }),
            (Func<IReadOnlyCollection<string>>)(()=> { probes++; if(failProcesses) throw new InvalidOperationException("Generated unavailable process probe"); return processes; }),
            (Action<bool,bool>)((system,screen)=> { if(refuseRequest) throw new InvalidOperationException("Generated refused wake request"); writes.Add((system,screen)); }));
        awake.GetType().GetEvent("Error")!.AddEventHandler(awake,(Action<string>)(error=>errors.Add(error)));
        try
        {
            Call(awake,"Hold",TimeSpan.FromMinutes(1)); Require(probes==0 && writes.Count==0,"An inactive service queried rules or applied a request.");
            Call(awake,"Start"); var timer=(DispatcherQueueTimer)Field(awake,"timer")!;
            Call(awake,"Start"); Require(ReferenceEquals(timer,Field(awake,"timer")) && timer.IsRunning,"Start duplicated or omitted its timer.");
            Call(awake,"Hold",TimeSpan.FromMinutes(2)); Require((bool)Get(awake,"IsAwake")! && writes[^1]==(true,false),"Timer did not request system-only wakefulness.");
            clock.Advance(TimeSpan.FromSeconds(30)); clock.Calendar+=TimeSpan.FromDays(10); Call(awake,"Evaluate");
            ac=false; Call(awake,"Evaluate");
            Require(!(bool)Get(awake,"IsAwake")! && (bool)Get(awake,"HasTimer")! && (string)Get(awake,"Status")! =="Paused on battery","Battery did not pause the request while retaining its timer.");
            clock.Advance(TimeSpan.FromSeconds(90)); ac=true; Call(awake,"Evaluate");
            Require(!(bool)Get(awake,"HasTimer")! && !(bool)Get(awake,"IsAwake")!,"An expired battery-paused timer restarted on AC.");
            settings.AwakeApps=["C:\\tools\\render.EXE"]; processes=["Render"]; Call(awake,"Evaluate");
            Require((bool)Get(awake,"IsAwake")! && (string)Get(awake,"Status")! =="App rule active","Case/path application rule did not match.");
            Call(awake,"Hold",TimeSpan.FromMinutes(1)); Call(awake,"Release");
            Require((bool)Get(awake,"IsAwake")! && !(bool)Get(awake,"HasTimer")!,"Stopping a timer removed an independent application rule.");
            ac=null; Call(awake,"Evaluate"); Require(!(bool)Get(awake,"IsAwake")! && (string)Get(awake,"Status")! =="Paused: power status unavailable","Unknown AC state bypassed AC-only.");
            ac=true; settings.AwakeWithExternalDisplay=true; display=true; settings.KeepDisplayAwake=true; Call(awake,"Evaluate");
            Require((string)Get(awake,"Status")! =="Display rule active" && writes[^1]==(true,true),"Display rule or display wake option failed.");
            settings.KeepDisplayAwake=false; Call(awake,"Evaluate"); Require(writes[^1]==(true,false),"Display-off option retained the display requirement.");
            display=false; processes=[]; Call(awake,"Evaluate"); Require(!(bool)Get(awake,"IsAwake")!,"Removed rules retained a request.");
            processes=["Render"]; Call(awake,"Evaluate"); failProcesses=true; Call(awake,"Evaluate");
            Require(!(bool)Get(awake,"IsAwake")! && errors.Count>0,"A failed process probe retained an unverified application request.");
            failProcesses=false; Call(awake,"Evaluate"); failAc=true; Call(awake,"Evaluate");
            Require(!(bool)Get(awake,"IsAwake")! && (string)Get(awake,"Status")! =="Paused: power status unavailable","A failed AC probe bypassed AC-only.");
            failAc=false; failDisplay=true; processes=[]; Call(awake,"Evaluate");
            Require(!(bool)Get(awake,"IsAwake")! && errors.Count>=3,"A failed display probe retained a request or skipped its notice.");
            failDisplay=false; settings.AwakeApps.Clear(); settings.AwakeWithExternalDisplay=false; refuseRequest=true; bool refused=false;
            try { Call(awake,"Hold",TimeSpan.FromMinutes(1)); }
            catch(TargetInvocationException error) when(error.InnerException is InvalidOperationException) { refused=true; }
            Require(refused && !(bool)Get(awake,"IsAwake")! && (string)Get(awake,"Status")! =="Request unavailable","A refused wake API call was presented as applied.");
            refuseRequest=false; Call(awake,"Evaluate"); Require((bool)Get(awake,"IsAwake")!,"A temporary wake failure could not recover.");
            Call(awake,"Hold",TimeSpan.FromMinutes(1)); Call(awake,"Stop");
            Require(!timer.IsRunning && Field(awake,"timer") is null && !(bool)Get(awake,"HasTimer")!,"Stop retained its timer/deadline.");
            int beforeProbes=probes,beforeWrites=writes.Count;
            Call(awake,"Tick",timer,new object()); Call(awake,"Hold",TimeSpan.FromMinutes(1));
            Require(probes==beforeProbes && writes.Count==beforeWrites && !(bool)Get(awake,"IsAwake")!,"A stale callback or action revived a stopped service.");
            Call(awake,"Start"); Require(!ReferenceEquals(timer,Field(awake,"timer")) && !(bool)Get(awake,"HasTimer")!,"Restart reused an old timer/deadline.");
            Call(awake,"Stop");
        }
        finally { ((IDisposable)awake).Dispose(); }
        results.Add("PASS production Keep awake with injected clock/power/process/display adapters: monotonic expiry, battery/unknown AC, independent rules, display flag changes, idempotent start, stopped timer and stale callback rejection.");

        // A short real request is confined to this fixture's UI thread. Restore
        // its previous flags even if an assertion fails; never alter a power plan.
        uint previous=SetThreadExecutionState(0x80000000);
        Require(previous!=0,"Cannot establish the fixture thread's initial wake state.");
        object native=Create("Vela.Windows.Services.AwakeService",(Func<AppSettings>)(()=>new AppSettings { EnabledFeatures=[] }),DispatcherQueue.GetForCurrentThread());
        try
        {
            Call(native,"Start"); Call(native,"Hold",TimeSpan.FromMilliseconds(100));
            uint active=SetThreadExecutionState(0x80000001);
            Require((active&3)==1 && (bool)Get(native,"IsAwake")!,"Native system-only execution flags were not applied.");
            var deadline=System.Diagnostics.Stopwatch.StartNew();
            while((bool)Get(native,"IsAwake")! && deadline.Elapsed<TimeSpan.FromSeconds(12)) await Task.Delay(50);
            Require(!(bool)Get(native,"IsAwake")! && (SetThreadExecutionState(0x80000000)&3)==0,"The real dispatcher timer did not release its expired native request.");
            Call(native,"Hold",TimeSpan.FromSeconds(10)); Call(native,"Stop");
            Require((SetThreadExecutionState(0x80000000)&3)==0,"Stop retained a native wake requirement.");
            results.Add("PASS real fixture-thread Windows execution-state flags and default five-second dispatcher callback: a 100 ms timer expires, native request releases, Stop releases a second request and prior thread flags are restored. No Sleep/lid/power-plan test is claimed.");
        }
        finally { try { ((IDisposable)native).Dispose(); } finally { SetThreadExecutionState(previous|0x80000000); } }
        await AwakePanelControls();
    }
    private static async Task AwakePanelControls()
    {
        Step("Main panel Stop timer remains usable for a battery-paused timer");
        new SettingsStore(Path.Combine(profile,"settings.json")).Save(new AppSettings { OnboardingComplete=true,EnabledFeatures=[],ShelfDropTarget="manual" });
        object controller=Create("Vela.Windows.AppController"); object? fake=null;
        try
        {
            Call(controller,"Initialize",true); object panel=Get(controller,"Panel")!;
            fake=Create("Vela.Windows.Services.AwakeService",(Func<AppSettings>)(()=>new AppSettings { AwakeOnlyOnAc=true }),DispatcherQueue.GetForCurrentThread(),new FixtureClock(),
                (Func<bool?>)(()=>false),(Func<bool>)(()=>false),(Func<IReadOnlyCollection<string>>)(()=>Array.Empty<string>()),(Action<bool,bool>)((_,_)=>{}));
            controller.GetType().GetProperty("Awake",Members)!.SetValue(controller,fake);
            Action refresh=()=>Call(panel,"RefreshAwake"); fake.GetType().GetEvent("Changed")!.AddEventHandler(fake,refresh);
            Call(fake,"Start"); Call(fake,"Hold",TimeSpan.FromMinutes(1));
            var stop=(Button)Field(panel,"StopAwake")!;
            Require(stop.IsEnabled && (string)stop.Content=="Stop timer" && !(bool)Get(fake,"IsAwake")!,"Paused timer cannot be canceled from the panel.");
            Call(panel,"StopAwakeClick",null,new RoutedEventArgs());
            Require(!stop.IsEnabled && !(bool)Get(fake,"HasTimer")!,"The panel handler failed to stop a paused timer.");
            results.Add("PASS real main-panel control state and stop handler against an injected battery-paused service. This is component evidence, not a physical button click.");
            await Async(controller,"FinishOperations");
            Call(panel,"StartAwakeClick",new Button { Tag="15" },new RoutedEventArgs());
            Require(!(bool)Get(fake,"HasTimer")!,"A queued panel action admitted a timer after Exit began.");
        }
        finally { (fake as IDisposable)?.Dispose(); ((IDisposable)controller).Dispose(); }
    }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern uint SetThreadExecutionState(uint flags);
    private static async Task WheelDelivery()
    {
        Step("Native wheel delivery to two owned windows; no global hooks or input injection");
        var one=new Window { Content=new Grid() }; var two=new Window { Content=new Grid() };
        object? input=null; IDisposable? oneMessages=null,twoMessages=null;
        try
        {
            one.Activate(); two.Activate(); one.AppWindow.Hide(); two.AppWindow.Hide();
            nint oneHandle=WinRT.Interop.WindowNative.GetWindowHandle(one),twoHandle=WinRT.Interop.WindowNative.GetWindowHandle(two);
            oneMessages=(IDisposable)Create("Vela.Windows.Interop.WindowMessages",oneHandle);
            twoMessages=(IDisposable)Create("Vela.Windows.Interop.WindowMessages",twoHandle);
            long oneTotal=0,twoTotal=0; int received=0;
            Action<uint,nuint,nint> first=(message,wp,_)=> { if(message==0x20A) { oneTotal+=(short)((ulong)wp>>16); received++; } };
            Action<uint,nuint,nint> second=(message,wp,_)=> { if(message==0x20A) { twoTotal+=(short)((ulong)wp>>16); received++; } };
            oneMessages.GetType().GetEvent("Message")!.AddEventHandler(oneMessages,first);
            twoMessages.GetType().GetEvent("Message")!.AddEventHandler(twoMessages,second);
            var settings=new AppSettings { EnabledFeatures=[] };
            input=Create("Vela.Windows.Services.InputService",(Func<AppSettings>)(()=>settings),DispatcherQueue.GetForCurrentThread(),(Func<Func<Task>,Task>)(action=>action()),oneHandle);
            // Mark only the component callback active; never install a system hook.
            var active=(HashSet<string>)Field(input,"active")!; active.Add("smoothScroll");
            var scroll=(SmoothScrollBuffer)Field(input,"scroll")!;
            void Tick()=>Call(input,"ScrollTick",null,new object());
            scroll.Add(oneHandle,120,1); Tick();
            scroll.Add(twoHandle,-120,1); int ticks=0;
            while(scroll.Pending) { Require(++ticks<2000,"Wheel did not drain."); Tick(); }
            await Task.Delay(100);
            Require(oneTotal>0 && oneTotal<120 && twoTotal==-120,"Queued wheel distance crossed destinations or the native signed delta changed.");
            twoTotal=0;
            for(int index=0;index<100;index++) scroll.Add(twoHandle,32767,3);
            while(scroll.Pending) { Require(++ticks<2000,"Wheel burst did not drain."); Tick(); }
            await Task.Delay(100);
            Require(twoTotal==100L*32767*3,"A native wheel burst wrapped, reversed or lost its signed message delta.");
            scroll.Add(oneHandle,120,1); active.Clear(); Call(input,"OptionsChanged");
            Require(!scroll.Pending,"Option changes left queued scrolling.");
            var cleaningKeys=(HashSet<uint>)Field(input,"cleaningKeys")!;
            var closeKeys=(HashSet<uint>)Field(input,"closeKeys")!;
            var suppressedUps=(HashSet<uint>)Field(input,"suppressedUps")!;
            var mappedButtons=(HashSet<uint>)Field(input,"mappedButtons")!;
            var lastClicks=(Dictionary<uint,long>)Field(input,"lastClicks")!;
            var closeGuard=(CloseShortcutGuard)Field(input,"closeGuard")!;
            var superKey=(SuperKeyState)Field(input,"superKey")!;
            cleaningKeys.Add(0x41); closeKeys.Add(0x73);
            suppressedUps.Add(0x202); mappedButtons.Add(1); lastClicks[0x201]=123;
            closeGuard.Allow("fixture",100,false); superKey.PressCaps();
            SetField(input,"lastWindow",oneHandle); SetField(input,"lastFocus",oneHandle);
            scroll.Add(oneHandle,120,1); Call(input,"SuspendInput");
            Require(!scroll.Pending && (bool)Field(input,"suspended")! && cleaningKeys.Count==0 && closeKeys.Count==0,"Suspend retained stale key guards or queued input.");
            Require(suppressedUps.Count==0 && mappedButtons.Count==0 && lastClicks.Count==0 && !closeGuard.Allow("fixture",200,false),"Suspend retained mouse pairing, debounce or close confirmation state.");
            Require((nint)Field(input,"lastWindow")! ==0 && (nint)Field(input,"lastFocus")! ==0 && superKey.ReleaseCaps()==(false,false),"Suspend retained a focus snapshot or allowed an unmatched Caps Lock release to become Escape.");
            Call(input,"ResumeInput"); Require(!(bool)Field(input,"suspended")!,"Resume failed.");
            Require((nint)Field(input,"keyboardHook")! ==0 && (nint)Field(input,"mouseHook")! ==0,"The component fixture installed a global hook.");
            results.Add("PASS actual native wheel messages: destination change, 9,830,100-tick burst, option clearing and suspend/resume; "+received+" messages received by owned windows.");
        }
        finally { (input as IDisposable)?.Dispose(); oneMessages?.Dispose(); twoMessages?.Dispose(); one.Close(); two.Close(); }
    }
    private static async Task SettingsImportTransaction()
    {
        Step("Settings import persistence failure and independent successful replacement");
        string path=Path.Combine(profile,"settings.json");
        new SettingsStore(path).Save(new AppSettings { OnboardingComplete=true,OnboardingStep=2,EnabledFeatures=[],Theme="Light",ShelfDropTarget="manual" });
        object controller=Create("Vela.Windows.AppController");
        try
        {
            Call(controller,"Initialize",true);
            var before=(AppSettings)Get(controller,"Settings")!;
            byte[] original=File.ReadAllBytes(path);
            var imported=new AppSettings { OnboardingComplete=false,OnboardingStep=0,EnabledFeatures=["imagePins"],Theme="Dark",ShelfDropTarget="manual",Snippets=[new("Imported",";imported","Preserved text")] };
            bool refused=false;
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                try { Call(controller,"Import",imported); }
                catch(TargetInvocationException error) when(error.InnerException is IOException or UnauthorizedAccessException) { refused=true; }
            }
            Require(refused && ReferenceEquals(before,Get(controller,"Settings")) && File.ReadAllBytes(path).SequenceEqual(original),"A failed import replaced live settings or claimed success before persistence.");
            Require(!imported.OnboardingComplete && imported.OnboardingStep==0,"Import modified the reviewed backup object.");
            Call(controller,"Import",imported);
            var accepted=(AppSettings)Get(controller,"Settings")!;
            Require(!ReferenceEquals(accepted,imported) && accepted.OnboardingComplete && accepted.OnboardingStep==2 && accepted.Theme=="Dark" && new SettingsStore(path).Load().Theme=="Dark","Successful import did not persist an independent replacement or preserve local onboarding.");
            imported.Snippets.Clear();
            Require(accepted.Snippets.Count==1 && accepted.EnabledFeatures.SetEquals(["imagePins"]),"Imported live settings alias the backup object.");
            Require((nint)Field(Get(controller,"Input")!,"keyboardHook")! ==0 && (nint)Field(Get(controller,"Input")!,"mouseHook")! ==0,"Settings import installed a global input hook.");
            results.Add("PASS failed settings import preserves exact live/disk settings and reviewed backup; successful import persists an independent copy and retains local onboarding.");
            await Async(controller,"FinishOperations");
        }
        finally { ((IDisposable)controller).Dispose(); }
    }
    private static async Task OnboardingGates()
    {
        Step("All 31 selected features remain inactive before onboarding completion");
        string path=Path.Combine(profile,"settings.json");
        new SettingsStore(path).Save(new AppSettings { OnboardingComplete=false,EnabledFeatures=FeatureCatalog.All.Select(feature=>feature.Id).ToHashSet(),ShelfDropTarget="manual" });
        object controller=Create("Vela.Windows.AppController");
        try
        {
            Call(controller,"Initialize",true);
            void Quiet()
            {
                object input=Get(controller,"Input")!,clipboard=Field(controller,"clipboard")!,shortcuts=Field(controller,"shortcuts")!,awake=Get(controller,"Awake")!;
                Require(((FeatureRuntime)Field(controller,"runtime")!).Running.Count==0 && (int)Get(Field(input,"active")!,"Count")! ==0,"Onboarding started a resident feature.");
                Require((nint)Field(input,"keyboardHook")! ==0 && (nint)Field(input,"mouseHook")! ==0,"Onboarding installed a global input hook.");
                Require((int)Get(Field(clipboard,"active")!,"Count")! ==0 && Field(clipboard,"timer") is null && (int)Get(Field(shortcuts,"registered")!,"Count")! ==0,"Onboarding started clipboard/shortcut listeners.");
                Require(Field(awake,"timer") is null && !(bool)Get(awake,"IsAwake")! && ((IDictionary)Field(Get(controller,"Dimmer")!,"overlays")!).Count==0,"Onboarding started wake requests, timers or overlays.");
                Require(Field(controller,"shelfDrops") is null && Field(controller,"thermalAlerts") is null && Field(controller,"resourceAlerts") is null,"Onboarding started shelf receiver or sensor services.");
            }
            Quiet();
            var settings=(AppSettings)Get(controller,"Settings")!;
            Require(settings.EnabledFeatures.Count==31 && !(bool)((ToggleSwitch)Field(Field(controller,"onboarding")!,"StartupToggle")!).IsEnabled,"Onboarding lost selections or allowed validation startup changes.");
            Call(controller,"Save",true); Quiet();
            Call(controller,"OpenTool","mediaTools"); Call(controller,"ShowShelf"); Call(controller,"ShowRadial");
            Require(Field(controller,"tools") is null && Field(controller,"shelfWindow") is null && Field(controller,"radial") is null,"A tool bypassed onboarding admission.");
            Require(new SettingsStore(path).Load().EnabledFeatures.Count==31,"Pre-onboarding selections were not persisted.");
            settings.EnabledFeatures.Clear(); Call(controller,"CompleteOnboarding"); Quiet();
            var saved=new SettingsStore(path).Load();
            Require(saved.OnboardingComplete && saved.OnboardingStep==3 && saved.EnabledFeatures.Count==0,"Completion did not persist the chosen empty selection.");
            results.Add("PASS all 31 selected tools stay inactive before onboarding, including Save/reapply and tool admission; selections persist, validation startup stays disabled, and completion with an empty selection starts no residents.");
            await Async(controller,"FinishOperations");
        }
        finally { ((IDisposable)controller).Dispose(); }
    }
    private static async Task SettingsTransferLifecycle()
    {
        Step("Tracked settings transfer cancellation on window close and controller Exit");
        string path=Path.Combine(profile,"settings.json"),backup=Path.Combine(profile,"generated-settings-backup.json");
        new SettingsStore(path).Save(new AppSettings { OnboardingComplete=true,EnabledFeatures=[],ShelfDropTarget="manual" });
        await SettingsBackup.Write(backup,new AppSettings { Theme="Dark",EnabledFeatures=[] });
        object controller=Create("Vela.Windows.AppController"); Task? pending=null;
        try
        {
            Call(controller,"Initialize",true);
            byte[] original=File.ReadAllBytes(path),originalBackup=File.ReadAllBytes(backup);
            string diagnostics=Path.Combine(profile,"startup-error.log");
            string previousDiagnostics=File.Exists(diagnostics)?File.ReadAllText(diagnostics):"";
            for(int pass=0;pass<2;pass++)
            {
                Call(controller,"ShowSettings"); object window=Field(controller,"settingsWindow")!;
                var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                bool canceled=false,applied=false,duplicateRan=false;
                Func<CancellationToken,Task> action=async token=>
                {
                    var imported=await SettingsBackup.Read(backup,token);
                    var root=(FrameworkElement)((Window)window).Content;
                    for(int wait=0;wait<100 && root.XamlRoot is null;wait++) await Task.Delay(10,token);
                    Require(root.XamlRoot is not null,"Settings window did not acquire a native XAML root.");
                    var dialog=new ContentDialog { XamlRoot=root.XamlRoot,Title="Generated import review",Content="Disposable settings fixture",PrimaryButtonText="Import",CloseButtonText="Cancel" };
                    SetField(window,"importDialog",dialog);
                    try
                    {
                        var shown=dialog.ShowAsync().AsTask(token); ready.SetResult();
                        var result=await shown; token.ThrowIfCancellationRequested();
                        if(result==ContentDialogResult.Primary) { Call(controller,"Import",imported); applied=true; }
                    }
                    catch(OperationCanceledException) { canceled=true; await Task.Delay(100); throw; }
                    finally { SetField(window,"importDialog",null); }
                };
                pending=Async(window,"TransferSettings",action);
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Require((int)Get(Field(controller,"operations")!,"Count")! ==1,"Settings work is not tracked by the controller.");
                await Async(window,"TransferSettings",(Func<CancellationToken,Task>)(_=> { duplicateRan=true; return Task.CompletedTask; }));
                Require(!duplicateRan,"A second settings transfer started concurrently.");
                if(pass==0) ((Window)window).Close(); else await Async(controller,"FinishOperations").WaitAsync(TimeSpan.FromSeconds(5));
                await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Require(canceled && !applied && Field(window,"transfer") is null && (int)Get(Field(controller,"operations")!,"Count")! ==0,"Close/Exit left settings work active or applied canceled data.");
                await Async(window,"TransferSettings",(Func<CancellationToken,Task>)(_=> { applied=true; return Task.CompletedTask; }));
                Require(!applied && File.ReadAllBytes(path).SequenceEqual(original) && File.ReadAllBytes(backup).SequenceEqual(originalBackup),"Closed/quitting settings work was admitted or modified an input.");
            }
            Require((File.Exists(diagnostics)?File.ReadAllText(diagnostics):"")==previousDiagnostics,"Ordinary settings cancellation was logged as an application failure.");
            results.Add("PASS real settings transfer tracking: generated backup read, pending native confirmation, duplicate admission blocked, close/Exit dismiss and cancel/await pending work, no late import, exact profile/backup bytes preserved. System picker and confirmation-button interactions are not claimed.");
        }
        finally
        {
            ((IDisposable)controller).Dispose();
            if(pending is not null) { try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch { } }
        }
    }
    private static async Task FaultedExitDiagnostics()
    {
        Step("Genuine operation faults remain recorded during controller shutdown");
        const string marker="Generated fixture operation failure";
        object controller=Create("Vela.Windows.AppController");
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task operation=Async(controller,"RunOperation",(Func<Task>)(async ()=> { await release.Task; throw new IOException(marker); }));
        try
        {
            Task finishing=Async(controller,"FinishOperations"); release.SetResult();
            await finishing.WaitAsync(TimeSpan.FromSeconds(5));
            bool failed=false; try { await operation; } catch(IOException error) when(error.Message==marker) { failed=true; }
            Require(failed && File.ReadAllText(Path.Combine(profile,"startup-error.log")).Contains(marker),"Shutdown swallowed a genuine operation fault.");
            results.Add("PASS expected canceled review work produces no failure log, while a deliberately faulted owned operation is recorded and drained.");
        }
        finally { release.TrySetResult(); ((IDisposable)controller).Dispose(); }
    }
    private static async Task ControllerLifecycle()
    {
        Step("Controller feature disable/re-enable with loaded shelf and pins");
        object controller=Create("Vela.Windows.AppController"); bool disposed=false;
        using var cancellation=new CancellationTokenSource();
        Task? operation=null;
        try
        {
            var settings=(AppSettings)Get(controller,"Settings")!;
            settings.EnabledFeatures.UnionWith(["shelf","imagePins","mediaTools"]); settings.ShelfDropTarget="manual";
            Call(controller,"Initialize",false);
            object shelf=Get(controller,"ShelfData")!, pins=Get(controller,"Pins")!;
            var data=new DataPackage(); data.SetText("Generated shelf entry 東京"); await Async(shelf,"Add",data.GetView(),CancellationToken.None);
            var items=(IList)Get(shelf,"Items")!; object kept=items[0]!;
            Call(controller,"ShowShelf");
            using(var bitmap=Bitmap()) await Async(pins,"Open",bitmap,"Generated controller fixture",CancellationToken.None);
            Require((int)Get(pins,"Count")! ==1 && Field(controller,"shelfWindow") is Window,"Loaded shelf/pin setup failed.");
            var token=(CancellationToken)Get(shelf,"PendingToken")!;
            Call(controller,"SetFeature","imagePins",false);
            Require((int)Get(pins,"Count")! ==0,"Feature disable left an image pin.");
            Call(controller,"SetFeature","shelf",false);
            Require(token.IsCancellationRequested && Field(controller,"shelfWindow") is null && items.Count==1 && ReferenceEquals(items[0],kept),"Shelf disable lost entries or kept its window/provider scope.");
            Call(controller,"SetFeature","shelf",true); Call(controller,"ShowShelf");
            Require(!((CancellationToken)Get(shelf,"PendingToken")!).IsCancellationRequested,"Shelf scope did not renew.");
            Require(items.Count==1 && ReferenceEquals(items[0],kept),"Shelf re-enable lost its session entry.");
            Call(controller,"SetFeature","imagePins",true);
            using(var bitmap=Bitmap()) await Async(pins,"Open",bitmap,"Generated exit pin",CancellationToken.None);
            results.Add("PASS real controller feature disable/re-enable closes loaded pin/shelf windows, cancels old provider scope and preserves the same shelf entry.");

            Step("Active OCR pipeline cancellation through controller FinishOperations");
            string sourcePath=Path.Combine(profile,"source.png"),destinationPath=Path.Combine(profile,"outputs"); Directory.CreateDirectory(destinationPath);
            var source=await StorageFile.GetFileFromPathAsync(CreateFile(sourcePath));
            using(var bitmap=Bitmap()) using(var stream=await source.OpenAsync(FileAccessMode.ReadWrite))
            { var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream); encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync(); }
            byte[] original=File.ReadAllBytes(sourcePath); var folder=await StorageFolder.GetFolderFromPathAsync(destinationPath);
            Call(controller,"OpenTool","mediaTools"); object tool=Field(controller,"tools")!; SetField(tool,"conversion",cancellation);
            var prepared=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); string? preparedInput=null; bool canceled=false;
            Func<string,string,CancellationToken,Task> recognize=async (input,_,token)=> { preparedInput=input; Require(File.Exists(input),"Prepared OCR input missing."); prepared.SetResult(); await Task.Delay(Timeout.Infinite,token); };
            Func<Task> action=async ()=>
            {
                try
                {
                    var service=typeof(App).Assembly.GetType("Vela.Windows.Services.MediaService")!;
                    await (Task)service.GetMethod("Ocr",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[new StorageFile[]{source},folder,(Action<string>)(_=>{}),cancellation.Token,recognize])!;
                }
                catch(OperationCanceledException) { canceled=true; }
                finally { SetField(tool,"conversion",null); }
            };
            operation=Async(controller,"RunOperation",action);
            await prepared.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Async(controller,"FinishOperations").WaitAsync(TimeSpan.FromSeconds(10));
            Require(canceled && operation.IsCompletedSuccessfully && cancellation.IsCancellationRequested,"Exit did not cancel and await active OCR.");
            Require(preparedInput is not null && !Directory.Exists(Path.GetDirectoryName(preparedInput)) && Directory.GetFiles(destinationPath).Length==0,"Canceled OCR left prepared/partial output.");
            Require(File.ReadAllBytes(sourcePath).SequenceEqual(original),"Controller exit changed a source image.");
            Require((bool)Get(controller,"Quitting")! && (int)Get(Field(controller,"operations")!,"Count")! ==0,"Quitting admitted or retained operations.");
            ((IDisposable)controller).Dispose(); disposed=true;
            Require((int)Get(pins,"Count")! ==0 && ((IList)Field(controller,"auxiliary")!).Count==0,"Dispose retained native windows/pin sources.");
            results.Add("PASS controller shutdown cancels and awaits the real OCR preparation pipeline with a blocked injected recognizer, removes owned temporary data, preserves source bytes and closes all loaded windows/pins. No physical picker or worker process is claimed by this case.");
        }
        finally
        {
            cancellation.Cancel();
            if(operation is not null) { try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch { } }
            if(!disposed)
                try { ((IDisposable)controller).Dispose(); }
                catch(Exception cleanup) { results.Add("INFO Cleanup after primary failure: "+cleanup); }
        }
    }
    private static string CreateFile(string path) { File.WriteAllBytes(path,[]); return path; }
}
internal sealed class FixtureApp : App
{
    public FixtureApp() : base(false) { }
    protected override void OnLaunched(LaunchActivatedEventArgs args) { }
}
internal sealed class FixtureClock : TimeProvider
{
    private long timestamp=1000;
    internal DateTimeOffset Calendar=new(2026,10,22,0,0,0,TimeSpan.Zero);
    public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
    public override long GetTimestamp()=>timestamp;
    public override DateTimeOffset GetUtcNow()=>Calendar;
    internal void Advance(TimeSpan duration)=>timestamp+=duration.Ticks;
}
