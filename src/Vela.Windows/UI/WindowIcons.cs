using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Vela.Windows.Interop;

namespace Vela.Windows.UI;

internal sealed class WindowIcons
{
    private static readonly ConditionalWeakTable<Window,WindowIcons> windows=new();
    private readonly Window window;
    private readonly FrameworkElement? root;
    private nint small,big;
    private (uint Small,uint Big,int Dpi) palette;
    private bool closed;

    private WindowIcons(Window window)
    {
        this.window=window; root=window.Content as FrameworkElement;
        window.Closed+=Closed;
        if(root is not null) root.ActualThemeChanged+=ThemeChanged;
    }
    internal static void Refresh(Window window)=>windows.GetValue(window,w=>new(w)).Update();
    private void ThemeChanged(FrameworkElement sender,object args)=>Update();
    private void Update()
    {
        if(closed) return;
        var smallColor=ThemedIcon.Foreground(root?.ActualTheme!=ElementTheme.Dark);
        var bigColor=ThemedIcon.Foreground(ThemedIcon.ShellIsLight);
        int dpi=(int)Native.GetDpiForWindow(WindowStyle.Handle(window));
        var next=(Small:Pack(smallColor),Big:Pack(bigColor),Dpi:dpi);
        if(small!=0 && palette==next) return;
        nint nextSmall=0,nextBig=0;
        try
        {
            nextSmall=ThemedIcon.Create(Math.Max(16,16*dpi/96),smallColor);
            nextBig=ThemedIcon.Create(Math.Max(32,32*dpi/96),bigColor);
            SendMessage(WindowStyle.Handle(window),0x0080,0,nextSmall); // WM_SETICON / ICON_SMALL
            SendMessage(WindowStyle.Handle(window),0x0080,1,nextBig); // ICON_BIG is used by the taskbar.
            if(small!=0) Native.DestroyIcon(small);
            if(big!=0) Native.DestroyIcon(big);
            small=nextSmall; big=nextBig; palette=next; nextSmall=nextBig=0;
        }
        finally
        {
            if(nextSmall!=0) Native.DestroyIcon(nextSmall);
            if(nextBig!=0) Native.DestroyIcon(nextBig);
        }
    }
    private static uint Pack(global::Windows.UI.Color color)=>(uint)(color.R<<16|color.G<<8|color.B);
    private void Closed(object sender,WindowEventArgs args)
    {
        closed=true; window.Closed-=Closed;
        if(root is not null) root.ActualThemeChanged-=ThemeChanged;
        if(small!=0) Native.DestroyIcon(small);
        if(big!=0) Native.DestroyIcon(big);
        windows.Remove(window);
    }
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd,uint message,nuint wp,nint lp);
}
