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
            track(window);
            try { window.Activate(); }
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
        var image=new Image { Source=source,Stretch=Stretch.Uniform };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(image,name);
        var card=new Border { Style=Ui.Style("VelaCard"),Padding=new Thickness(8),Child=image };
        Grid.SetRow(card,1); root.Children.Add(card);
        var caption=Ui.Text("Resize this window to change the image size. Esc closes this pin.","VelaCaption");
        Grid.SetRow(caption,2); root.Children.Add(caption);
        root.KeyDown+=(_,e)=> { if(e.Key==global::Windows.System.VirtualKey.Escape) { e.Handled=true; Close(); } };
        Content=root; WindowStyle.Configure(this,540,440); Title="Image pin · Vela";
        if(AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop=true;
            top.Checked+=(_,_)=>presenter.IsAlwaysOnTop=true;
            top.Unchecked+=(_,_)=>presenter.IsAlwaysOnTop=false;
        }
        Closed+=(_,_)=> { image.Source=null; source.Dispose(); };
        root.Loaded+=(_,_)=>top.Focus(FocusState.Programmatic);
    }
}
