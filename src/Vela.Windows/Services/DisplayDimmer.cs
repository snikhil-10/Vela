using System.Runtime.InteropServices;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed class DisplayDimmer : IFeatureService
{
    private readonly Dictionary<nint,nint> overlays=[];
    public string Id=>"displayDimmer";
    public IReadOnlyList<DisplayTarget> Displays()
    {
        var displays=new List<DisplayTarget>();
        Native.EnumDisplayMonitors(0,0,(nint monitor,nint hdc,ref Native.Rect rect,nint data)=>
        { displays.Add(new(monitor,$"Display {displays.Count+1} · {rect.Right-rect.Left} × {rect.Bottom-rect.Top}")); return true; },0);
        return displays;
    }
    public void Dim(nint monitor,int percent)
    {
        percent=Math.Clamp(percent,0,70);
        if(percent==0) { if(overlays.Remove(monitor,out var existing)) DestroyWindow(existing); return; }
        var info=new Native.MonitorInfo { Size=(uint)Marshal.SizeOf<Native.MonitorInfo>() };
        if(!Native.GetMonitorInfo(monitor,ref info)) return;
        if(!overlays.TryGetValue(monitor,out var window))
        {
            // STATIC's black rectangle is a plain Win32 surface. It does not capture the screen.
            window=CreateWindowEx(0x80000|0x20|0x80|0x08000000|0x8,"STATIC","",0x80000000|0x08000000|4,
                info.Monitor.Left,info.Monitor.Top,info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top,0,0,0,0);
            if(window==0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            overlays.Add(monitor,window);
        }
        Native.SetLayeredWindowAttributes(window,0,(byte)(percent*255/100),2);
        Native.SetWindowPos(window,-1,info.Monitor.Left,info.Monitor.Top,info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top,0x10|0x40);
    }
    public void Start() { }
    public void Stop() { foreach(var window in overlays.Values) DestroyWindow(window); overlays.Clear(); }
    public void Dispose()=>Stop();
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern nint CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int w,int h,nint parent,nint menu,nint instance,nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
}
internal sealed record DisplayTarget(nint Handle,string Name);
