using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Vela.Windows.Interop;

namespace Vela.Windows.UI;

// Only the drag receiver is transparent. Explorer continues drawing the real
// taskbar icon underneath it; the app's normal windows retain their Mica.
internal sealed class ShelfTargetBackdrop(nint handle) : SystemBackdrop
{
    private global::Windows.UI.Composition.Compositor? compositor;
    private global::Windows.UI.Composition.CompositionColorBrush? brush;
    private Native.SubclassProc? paint;

    internal static void RemoveFrame(nint window)
    {
        // A presenter without a title bar can still keep DWM's rounded outer
        // frame. This receiver needs a bare popup; XAML draws the inset cue.
        long style=(long)Native.GetWindowLongPtrW(window,-16);
        long bare=(style&~0x00CF0000L)|0x80000000L; // Strip caption/sizing/system menu; WS_POPUP.
        long extended=(long)Native.GetWindowLongPtrW(window,-20);
        long tool=(extended&~0x00060301L)|0x08000080L; // No raised/client/modal edges or APPWINDOW; NOACTIVATE | TOOLWINDOW.
        if(style!=bare) Native.SetWindowLongPtrW(window,-16,(nint)bare);
        if(extended!=tool) Native.SetWindowLongPtrW(window,-20,(nint)tool);
        uint square=1; // DWMWCP_DONOTROUND: the transparent HWND itself has no rounded frame or shadow.
        Native.DwmSetWindowAttribute(window,33,ref square,sizeof(uint));
        uint none=0xFFFFFFFE;
        Native.DwmSetWindowAttribute(window,34,ref none,sizeof(uint));
        if(style!=bare || extended!=tool)
            Native.SetWindowPos(window,0,0,0,0,0,0x0037); // FRAMECHANGED | NOMOVE | NOSIZE | NOZORDER | NOACTIVATE.
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target,XamlRoot root)
    {
        base.OnTargetConnected(target,root);
        if(!ConfigureAlpha()) throw new InvalidOperationException("The shelf drop target could not prepare its transparent background.");
        paint=Paint;
        if(!Native.SetWindowSubclass(handle,paint,72,0)) throw new System.ComponentModel.Win32Exception();
        compositor=new(); brush=compositor.CreateColorBrush(Microsoft.UI.Colors.Transparent);
        target.SystemBackdrop=brush;
        nint dc=GetDC(handle);
        try { if(dc!=0) Erase(dc); }
        finally { if(dc!=0) ReleaseDC(handle,dc); }
    }
    private bool ConfigureAlpha()
    {
        RemoveFrame(handle);
        var margins=new Margins();
        if(DwmExtendFrameIntoClientArea(handle,ref margins)<0) return false;
        nint region=CreateRectRgn(-2,-2,-1,-1);
        if(region==0) return false;
        try
        {
            // The off-window region enables per-pixel alpha without blurring
            // any taskbar pixels or introducing another material behind them.
            var alpha=new BlurBehind { Flags=3,Enable=true,Region=region };
            return DwmEnableBlurBehindWindow(handle,ref alpha)>=0;
        }
        finally { if(region!=0) Native.DeleteObject(region); }
    }
    private nint Paint(nint window,uint message,nuint wp,nint lp,nuint id,nuint data)
    {
        if(message==0x0014 && Erase((nint)wp)) return 1; // WM_ERASEBKGND
        if(message==0x031E) _=ConfigureAlpha(); // WM_DWMCOMPOSITIONCHANGED; never throw across the native callback.
        return Native.DefSubclassProc(window,message,wp,lp);
    }
    private bool Erase(nint dc)=>GetClientRect(handle,out var rect) && FillRect(dc,ref rect,GetStockObject(4))!=0;
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        if(paint is not null) Native.RemoveWindowSubclass(handle,paint,72);
        paint=null; target.SystemBackdrop=null;
        brush?.Dispose(); brush=null; compositor?.Dispose(); compositor=null;
        base.OnTargetDisconnected(target);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left,Right,Top,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BlurBehind
    { public uint Flags; [MarshalAs(UnmanagedType.Bool)] public bool Enable; public nint Region; [MarshalAs(UnmanagedType.Bool)] public bool Transition; }
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint hwnd,ref Margins margins);
    [DllImport("dwmapi.dll")] private static extern int DwmEnableBlurBehindWindow(nint hwnd,ref BlurBehind blur);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int index);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd,nint dc);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd,out Native.Rect rect);
    [DllImport("user32.dll")] private static extern int FillRect(nint dc,ref Native.Rect rect,nint brush);
}
