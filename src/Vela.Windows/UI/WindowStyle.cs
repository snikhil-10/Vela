using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Vela.Windows.Interop;
using Windows.Graphics;
using WinRT.Interop;

namespace Vela.Windows.UI;

internal static class WindowStyle
{
    public static nint Handle(Window window)=>WindowNative.GetWindowHandle(window);
    public static void Configure(Window window,int width,int height,bool compact=false)
    {
        window.Title="Vela"; window.SystemBackdrop=new MicaBackdrop();
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","Vela.ico"));
        Resize(window,width,height);
        if(!compact) Center(window,width,height);
        if(window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable=false;
            if(compact) { presenter.IsResizable=false; presenter.IsMinimizable=false; presenter.SetBorderAndTitleBar(true,false); window.AppWindow.IsShownInSwitchers=false; }
        }
        ApplyTheme(window,((App)Application.Current).Controller?.Settings.Theme??"System");
    }
    public static void Resize(Window window,int width,int height)
    {
        double scale=Math.Max(1,Native.GetDpiForWindow(Handle(window))/96.0);
        window.AppWindow.Resize(new((int)(width*scale),(int)(height*scale)));
    }
    public static void ApplyTheme(Window window,string value)
    {
        if(window.Content is FrameworkElement root) root.RequestedTheme=value switch { "Dark"=>ElementTheme.Dark,"Light"=>ElementTheme.Light,_=>ElementTheme.Default };
        window.AppWindow.TitleBar.PreferredTheme=value switch { "Dark"=>TitleBarTheme.Dark,"Light"=>TitleBarTheme.Light,_=>TitleBarTheme.UseDefaultAppMode };
        WindowIcons.Refresh(window);
    }
    private static void Center(Window window,int width,int height)
    {
        Native.GetCursorPos(out var point);
        var info=new Native.MonitorInfo { Size=(uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        if(!Native.GetMonitorInfo(Native.MonitorFromPoint(point,2),ref info)) return;
        window.AppWindow.Move(new(info.Work.Left+12,info.Work.Top+12));
        double scale=Native.GetDpiForWindow(Handle(window))/96.0;
        int w=Math.Min((int)(width*scale),info.Work.Right-info.Work.Left-24);
        int h=Math.Min((int)(height*scale),info.Work.Bottom-info.Work.Top-24);
        window.AppWindow.MoveAndResize(new(info.Work.Left+(info.Work.Right-info.Work.Left-w)/2,info.Work.Top+(info.Work.Bottom-info.Work.Top-h)/2,w,h));
    }
    public static void NearPointer(Window window,int width,int height,Native.Rect? anchor=null)
    {
        Native.GetCursorPos(out var point);
        if(anchor is {} bounds) point=new((bounds.Left+bounds.Right)/2,(bounds.Top+bounds.Bottom)/2);
        var monitor=Native.MonitorFromPoint(point,2);
        var info=new Native.MonitorInfo { Size=(uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        if(!Native.GetMonitorInfo(monitor,ref info)) return;
        // Move onto the destination monitor before asking Windows for that window's DPI.
        window.AppWindow.Move(new PointInt32(info.Work.Left+20,info.Work.Top+20));
        double scale=Native.GetDpiForWindow(Handle(window))/96.0;
        int w=(int)(width*scale),h=(int)(height*scale),gap=(int)(12*scale);
        w=Math.Min(w,info.Work.Right-info.Work.Left-gap*2); h=Math.Min(h,info.Work.Bottom-info.Work.Top-gap*2);
        int x=Math.Clamp(point.X-w+24,info.Work.Left+gap,info.Work.Right-w-gap);
        int y=Math.Clamp(point.Y-h-gap,info.Work.Top+gap,info.Work.Bottom-h-gap);
        window.AppWindow.MoveAndResize(new RectInt32(x,y,w,h));
    }
}
