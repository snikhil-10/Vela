using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Vela.Windows;

internal static class Program
{
    private static string log=Path.Combine(AppContext.BaseDirectory,"pin-smoke-result.log");
    [STAThread]
    private static void Main()
    {
        try
        {
            string profile=Path.Combine(AppContext.BaseDirectory,"profile"); Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile,"settings.json"),"{\"Version\":1,\"OnboardingComplete\":true,\"OnboardingStep\":3,\"EnabledFeatures\":[],\"Theme\":\"System\",\"PinFlyout\":true}");
            typeof(App).Assembly.GetType("Vela.Windows.AppDataPaths")!.GetMethod("Configure",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[new string[]{"--data-directory",profile}]);
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_=>
            {
                var queue=DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
                var app=new SmokeApp();
                queue.TryEnqueue(async ()=>
                {
                    Window? pin=null; var results=new List<string>();
                    try
                    {
                        await Task.Delay(300);
                        var controller=typeof(App).GetProperty("Controller",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(app);
                        var panel=(Window?)controller?.GetType().GetProperty("Panel")!.GetValue(controller);
                        panel?.AppWindow.Hide();
                        using var bitmap=new SoftwareBitmap(BitmapPixelFormat.Bgra8,320,200,BitmapAlphaMode.Premultiplied);
                        byte[] pixels=new byte[320*200*4]; for(int i=0;i<pixels.Length;i+=4) { pixels[i]=48; pixels[i+1]=96; pixels[i+2]=192; pixels[i+3]=255; } bitmap.CopyFromBuffer(pixels.AsBuffer());
                        var source=new SoftwareBitmapSource(); await source.SetBitmapAsync(bitmap);
                        pin=(Window)Activator.CreateInstance(typeof(App).Assembly.GetType("Vela.Windows.UI.ImagePinWindow")!,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[source,"Generated pin size fixture"],null)!;
                        pin.Activate(); await Task.Delay(300);
                        var presenter=(Microsoft.UI.Windowing.OverlappedPresenter)pin.AppWindow.Presenter;
                        double scale=((FrameworkElement)pin.Content).XamlRoot.RasterizationScale;
                        foreach(var size in new[]{new Windows.Graphics.SizeInt32(200,40),new(500,40),new(40,500)})
                        {
                            pin.AppWindow.Resize(size); await Task.Delay(150);
                            var actual=pin.AppWindow.Size;
                            var images=Images((DependencyObject)pin.Content).ToArray();
                            if(actual.Width<Math.Ceiling(320*scale) || actual.Height<Math.Ceiling(280*scale))throw new Exception("Native minimum did not clamp resize.");
                            if(images.Length!=1 || images[0].ActualHeight<96 || images[0].ActualWidth<96)throw new Exception("Image layout collapsed.");
                            var root=(FrameworkElement)pin.Content; var bounds=images[0].TransformToVisual(root).TransformBounds(new global::Windows.Foundation.Rect(0,0,images[0].ActualWidth,images[0].ActualHeight));
                            if(bounds.Top<0 || bounds.Bottom>root.ActualHeight || bounds.Left<0 || bounds.Right>root.ActualWidth)throw new Exception("Image extends outside visible client area.");
                            if(!presenter.IsAlwaysOnTop || !presenter.IsResizable)throw new Exception("Topmost or normal resize disabled.");
                            results.Add($"PASS requested={size.Width}x{size.Height}; actual={actual.Width}x{actual.Height}; scale={scale}; image={images[0].ActualWidth:F1}x{images[0].ActualHeight:F1}; image visible within client; topmost/resizable retained.");
                        }
                        pin.Close(); pin=null; results.Add("PASS pin minimum-size native layout smoke."); File.WriteAllLines(log,results);
                    }
                    catch(Exception error) { results.Add("FAIL "+error); File.WriteAllLines(log,results); Environment.ExitCode=1; }
                    finally
                    {
                        pin?.Close();
                        typeof(App).GetMethod("Quit",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(app,null);
                    }
                });
            });
        }
        catch(Exception error) { File.WriteAllText(log,"FAIL "+error); Environment.ExitCode=1; }
    }
    private static IEnumerable<Image> Images(DependencyObject node)
    {
        if(node is Image image) yield return image;
        for(int index=0;index<VisualTreeHelper.GetChildrenCount(node);index++)
            foreach(var found in Images(VisualTreeHelper.GetChild(node,index)))yield return found;
    }
}

internal sealed class SmokeApp : App
{
    public SmokeApp() : base(false) { }
    protected override void OnLaunched(LaunchActivatedEventArgs args) { }
}
