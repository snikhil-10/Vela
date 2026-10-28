using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Vela.Windows;

internal static class Program
{
    private const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static readonly List<string> results=[];
    private static readonly HashSet<Window> live=[];
    private static string log=Path.Combine(AppContext.BaseDirectory,"session-tools-result.log");
    [STAThread]
    private static void Main()
    {
        try
        {
            File.WriteAllText(log,"START native session-tool fixture\n");
            string profile=Path.Combine(AppContext.BaseDirectory,"profile"); Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile,"settings.json"),"{\"Version\":1,\"OnboardingComplete\":true,\"OnboardingStep\":3,\"EnabledFeatures\":[],\"Theme\":\"System\"}");
            typeof(App).Assembly.GetType("Vela.Windows.AppDataPaths")!.GetMethod("Configure",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[new string[]{"--data-directory",profile}]);
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_=>
            {
                var queue=DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
                var app=new FixtureApp();
                queue.TryEnqueue(async ()=>
                {
                    try
                    {
                        // Keep a dispatcher-owned window alive between component close/reopen checks.
                        var anchor=new Window { Content=new Grid() }; Track(anchor);
                        anchor.Activate(); anchor.AppWindow.Hide();
                        Step("Shelf search");
                        await ShelfSearch();
                        Step("Pin lifecycle");
                        await PinLifecycle();
                        Step("Pin tracking failure");
                        await PinTrackingFailure();
                        results.Add("PASS native session-tool component checks.");
                    }
                    catch(Exception error) { results.Add("FAIL "+error); Environment.ExitCode=1; }
                    finally
                    {
                        foreach(var window in live.ToArray()) window.Close();
                        File.WriteAllLines(log,results);
                        typeof(App).GetMethod("Quit",Members)!.Invoke(app,null);
                    }
                });
            });
        }
        catch(Exception error) { File.WriteAllText(log,"FAIL "+error); Environment.ExitCode=1; }
    }
    private static object Create(string type,params object?[] args)=>Activator.CreateInstance(typeof(App).Assembly.GetType(type)!,Members,null,args,null)!;
    private static object? Get(object instance,string property)=>instance.GetType().GetProperty(property,Members)!.GetValue(instance);
    private static object? Call(object instance,string method,params object?[] args)=>instance.GetType().GetMethod(method,Members)!.Invoke(instance,args);
    private static Task Async(object instance,string method,params object?[] args)=>(Task)Call(instance,method,args)!;
    private static void Require(bool condition,string reason) { if(!condition) throw new InvalidOperationException(reason); }
    private static void Step(string name) { results.Add("STEP "+name); File.WriteAllLines(log,results); }
    private static void Track(Window window) { live.Add(window); window.Closed+=(_,_)=>live.Remove(window); }
    private static Image PinImage(Window window)=>(Image)((Grid)window.Content).Children.OfType<Border>().Single().Child;
    private static int PinCount(object pins)=>(int)Get(pins,"Count")!;
    private static async Task Open(object pins)
    {
        // Real pin actions decode a fresh bitmap for each request. A disposed
        // SoftwareBitmapSource can invalidate a reused native bitmap.
        using var bitmap=Bitmap();
        await Async(pins,"Open",bitmap,"Generated lifecycle fixture",CancellationToken.None);
    }
    private static IEnumerable<T> Children<T>(DependencyObject node) where T:DependencyObject
    {
        if(node is T match) yield return match;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            foreach(var child in Children<T>(VisualTreeHelper.GetChild(node,i))) yield return child;
    }
    private static async Task ShelfSearch()
    {
        object controller=Create("Vela.Windows.AppController");
        object shelf=Get(controller,"ShelfData")!;
        var items=(IList)Get(shelf,"Items")!;
        async Task Add(string text)
        { var package=new DataPackage(); package.SetText(text); await Async(shelf,"Add",package.GetView(),CancellationToken.None); }
        var window=(Window)Create("Vela.Windows.UI.ToolWindow",controller,"shelf"); Track(window);
        Step("Shelf native window constructed");
        try
        {
            window.Activate(); await Task.Delay(150);
            var search=Children<TextBox>((DependencyObject)window.Content).Single(box=>AutomationProperties.GetName(box)=="Find shelf items");
            var list=Children<ListView>((DependencyObject)window.Content).Single();
            var summary=Children<TextBlock>((DependencyObject)window.Content).Single(text=>AutomationProperties.GetName(text)=="Shelf search results");
            Require(list.Items.Count==0 && summary.Text.StartsWith("The shelf is empty."),"Empty shelf state missing.");
            await Add("April report 東京"); await Add("Hidden notes"); await Add("May report");
            object first=items[0]!, hidden=items[1]!, last=items[2]!;
            Require(list.Items.Count==3,"Initial shelf rows missing.");
            list.SelectedItems.Add(first); list.SelectedItems.Add(last);
            search.Text="report"; await Task.Delay(50);
            Require(list.Items.Count==2 && ReferenceEquals(list.Items[0],first) && ReferenceEquals(list.Items[1],last) && list.SelectedItems.Count==2,"Matching selection/order did not survive filtering.");
            search.Text="東京"; await Task.Delay(50);
            Require(list.Items.Count==1 && ReferenceEquals(list.SelectedItem,first),"Hidden selected row remained selected.");
            search.Text=""; await Task.Delay(50);
            Require(list.Items.Count==3 && list.SelectedItems.Count==1 && ReferenceEquals(list.SelectedItem,first),"Clearing search restored a hidden selection.");
            search.Text="no-match"; await Task.Delay(50);
            Require(list.Items.Count==0 && items.Count==3 && summary.Text.StartsWith("No matches."),"Empty search removed entries or omitted empty state.");
            await Add("Live report");
            Require(items.Count==4 && list.Items.Count==0,"An addition escaped the active filter.");
            search.Text="report"; await Task.Delay(50);
            Require(list.Items.Count==3 && list.SelectedItems.Count==0,"Added matching entry missing or hidden selection resurrected.");
            list.SelectedItems.Add(last); items.Remove(first); await Task.Delay(50);
            Require(list.Items.Count==2 && ReferenceEquals(list.SelectedItem,last),"Collection update lost the visible selection.");
            items.Remove(last); await Task.Delay(50);
            Require(list.Items.Count==1 && items.Contains(hidden) && list.SelectedItems.Count==0,"Removing a visible entry touched hidden rows.");
            items.Clear(); await Task.Delay(50);
            Require(list.Items.Count==0 && summary.Text.StartsWith("The shelf is empty."),"A collection reset left stale search rows or counts.");
            int beforeClose=list.Items.Count;
            window.Close(); await Add("After close report");
            Require(list.Items.Count==beforeClose,"Closed shelf remained subscribed to collection changes.");
            results.Add("PASS native shelf filter: live add/remove/reset, Unicode, empty/clear, stable order, visible-only selection, hidden-entry retention and close unsubscribe.");
        }
        finally { if(live.Contains(window)) window.Close(); }
    }
    private static SoftwareBitmap Bitmap()
    {
        var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,128,96,BitmapAlphaMode.Premultiplied);
        byte[] pixels=new byte[128*96*4]; for(int i=0;i<pixels.Length;i+=4) { pixels[i]=48; pixels[i+1]=96; pixels[i+2]=192; pixels[i+3]=255; }
        bitmap.CopyFromBuffer(pixels.AsBuffer()); return bitmap;
    }
    private static async Task PinLifecycle()
    {
        bool enabled=true; var tracked=new List<Window>();
        Action<Window> track=window=> { tracked.Add(window); Track(window); };
        object pins=Create("Vela.Windows.UI.ImagePins",track,(Func<bool>)(()=>enabled));
        try
        {
            for(int i=0;i<6;i++) await Open(pins);
            await Task.Delay(100);
            Require(PinCount(pins)==6 && tracked.Count==6 && tracked.All(window=>window.AppWindow.IsVisible && PinImage(window).Source is not null && PinImage(window).ActualWidth>0 && PinImage(window).ActualHeight>0),"Six native pins did not show image layout.");
            bool rejected=false;
            try { await Open(pins); } catch(InvalidOperationException) { rejected=true; }
            Require(rejected && PinCount(pins)==6 && tracked.Count==6,"Seventh pin exceeded capacity or evicted an existing pin.");
            var firstImage=PinImage(tracked[0]); tracked[0].Close();
            Require(PinCount(pins)==5 && firstImage.Source is null,"Closing one pin did not release capacity/image source.");
            await Open(pins); Require(PinCount(pins)==6 && tracked.Count==7,"Closing a pin did not permit a replacement.");
            var active=tracked.Where(live.Contains).ToArray(); var sources=active.Select(PinImage).ToArray();
            Call(pins,"CloseAll"); Call(pins,"CloseAll");
            Require(PinCount(pins)==0 && active.All(window=>!live.Contains(window)) && sources.All(image=>image.Source is null),"CloseAll did not close/release every pin or was not idempotent.");
            enabled=false; await Open(pins);
            Require(PinCount(pins)==0 && tracked.Count==7,"Disabled pin feature accepted a new image.");
            enabled=true; using var cancellation=new CancellationTokenSource(); cancellation.Cancel(); bool canceled=false;
            using var canceledBitmap=Bitmap();
            try { await Async(pins,"Open",canceledBitmap,"Canceled fixture",cancellation.Token); } catch(OperationCanceledException) { canceled=true; }
            Require(canceled && PinCount(pins)==0 && tracked.Count==7,"Canceled pin preparation leaked a window.");
            await Open(pins); Call(pins,"CloseAll");
            Require(PinCount(pins)==0,"A canceled/disabled open poisoned the next session.");
            results.Add("PASS native pins: six visible, seventh rejected, close/reopen capacity, cleared image sources, idempotent CloseAll, disabled/canceled admission and fresh-session recovery.");
        }
        finally { Call(pins,"CloseAll"); }
    }
    private static async Task PinTrackingFailure()
    {
        Window? rejected=null; Image? image=null;
        Action<Window> fail=window=> { rejected=window; image=PinImage(window); Track(window); throw new InvalidOperationException("Generated tracking failure"); };
        object pins=Create("Vela.Windows.UI.ImagePins",fail,(Func<bool>)(()=>true));
        try
        {
            bool failed=false; try { await Open(pins); } catch(InvalidOperationException) { failed=true; }
            Require(failed && PinCount(pins)==0 && rejected is not null && !live.Contains(rejected) && image?.Source is null,"A failed window-registration callback retained a pin and its image source.");
            results.Add("PASS native pin failure cleanup: tracking exception closes window, releases source and restores capacity.");
        }
        finally { Call(pins,"CloseAll"); }
    }
}

internal sealed class FixtureApp : App
{
    public FixtureApp() : base(false) { }
    protected override void OnLaunched(LaunchActivatedEventArgs args) { }
}
