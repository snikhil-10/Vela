using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Vela.Windows.UI;

internal sealed class ImagePins(Action<Window> track,Func<bool> enabled)
{
    public const int Limit=6;
    private readonly List<ImagePinWindow> windows=[];
    public int Count=>windows.Count;
    public async Task Open(SoftwareBitmap bitmap,string name,CancellationToken cancellation)
    {
        var source=new SoftwareBitmapSource(); bool transferred=false;
        try
        {
            await source.SetBitmapAsync(bitmap);
            cancellation.ThrowIfCancellationRequested();
            if(!enabled()) return;
            if(windows.Count>=Limit) throw new InvalidOperationException("Close an image pin before adding another. Up to six can stay open.");
            var window=new ImagePinWindow(source,name);
            transferred=true; windows.Add(window); window.Closed+=(_,_)=>windows.Remove(window);
            try { track(window); window.Activate(); }
            catch { window.Close(); throw; }
        }
        finally { if(!transferred) source.Dispose(); }
    }
    public void CloseAll() { foreach(var window in windows.ToArray()) window.Close(); }
}

internal sealed class ImagePinWindow : Window
{
    public ImagePinWindow(SoftwareBitmapSource source,string name)
    {
        var root=new Grid { Padding=new Thickness(12),RowSpacing=12 };
        root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        root.RowDefinitions.Add(new() { Height=new GridLength(1,GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        var top=new ToggleButton { Content="Keep on top",IsChecked=true };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(top,"Keep this image above other windows");
        actions.Children.Add(top); actions.Children.Add(Ui.Button("Close pin",Close)); root.Children.Add(actions);
        var image=new Image { Source=source,Stretch=Stretch.Uniform,MinHeight=96,MinWidth=96 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(image,name);
        var card=new Border { Style=Ui.Style("VelaCard"),Padding=new Thickness(8),Child=image };
        Grid.SetRow(card,1); root.Children.Add(card);
        var caption=Ui.Text("Resize this window to change the image size. Esc closes this pin.","VelaCaption");
        Grid.SetRow(caption,2); root.Children.Add(caption);
        root.KeyDown+=(_,e)=> { if(e.Key==global::Windows.System.VirtualKey.Escape) { e.Handled=true; Close(); } };
        Content=root; WindowStyle.Configure(this,540,440); Title="Image pin · Vela";
        XamlRoot? pinRoot=null;
        global::Windows.Foundation.TypedEventHandler<XamlRoot,XamlRootChangedEventArgs>? rootChanged=null;
        if(AppWindow.Presenter is OverlappedPresenter presenter)
        {
            void MinimumSize()
            {
                // AppWindow uses physical pixels; keep room for controls and the image at each DPI.
                double scale=root.XamlRoot?.RasterizationScale??Math.Max(1,Vela.Windows.Interop.Native.GetDpiForWindow(WindowStyle.Handle(this))/96.0);
                presenter.PreferredMinimumWidth=(int)Math.Ceiling(320*scale);
                presenter.PreferredMinimumHeight=(int)Math.Ceiling(280*scale);
            }
            MinimumSize();
            rootChanged=(_,_)=>MinimumSize();
            root.Loaded+=(_,_)=>
            {
                if(pinRoot is not null) pinRoot.Changed-=rootChanged;
                pinRoot=root.XamlRoot;
                if(pinRoot is not null) pinRoot.Changed+=rootChanged;
                MinimumSize();
            };
            presenter.IsAlwaysOnTop=true;
            top.Checked+=(_,_)=>presenter.IsAlwaysOnTop=true;
            top.Unchecked+=(_,_)=>presenter.IsAlwaysOnTop=false;
        }
        Closed+=(_,_)=> { if(pinRoot is not null && rootChanged is not null) pinRoot.Changed-=rootChanged; image.Source=null; source.Dispose(); };
        root.Loaded+=(_,_)=>top.Focus(FocusState.Programmatic);
    }
}
