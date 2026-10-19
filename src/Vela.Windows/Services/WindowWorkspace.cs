using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed record DesktopDisplay(nint Handle,string Id,string Name,Native.Rect Area,Native.Rect Work);
internal sealed record OpenWindow(nint Handle,uint ProcessId,string Process,string Title,DesktopDisplay Monitor,Native.Rect Bounds,bool Maximized)
{ public string Display=>$"{Title} · {Process}"; }

internal sealed class WindowWorkspace : IDisposable
{
    private readonly Dictionary<nint,OpenWindow> pinned=[];
    private readonly Dictionary<nint,(OpenWindow Window,DesktopNative.Placement Placement)> undo=[];
    public bool CanUndo=>undo.Count>0;
    public static IReadOnlyList<DesktopDisplay> Monitors()
    {
        var result=new List<DesktopDisplay>();
        Native.MonitorEnum callback=(nint h,nint dc,ref Native.Rect r,nint data)=>
        {
            var info=new DesktopNative.MonitorEx { Size=(uint)Marshal.SizeOf<DesktopNative.MonitorEx>(),Device="" };
            if(DesktopNative.GetMonitorInfo(h,ref info)) result.Add(new(h,info.Device,$"Display {result.Count+1}"+((info.Flags&1)!=0?" · primary":""),info.Monitor,info.Work));
            return true;
        };
        Native.EnumDisplayMonitors(0,0,callback,0); return result;
    }
    public IReadOnlyList<OpenWindow> Windows()
    {
        var monitors=Monitors(); var result=new List<OpenWindow>();
        DesktopNative.WindowEnum callback=(h,_)=>
        {
            if(!DesktopNative.IsWindowVisible(h) || DesktopNative.GetWindow(h,4)!=0 || ((long)Native.GetWindowLongPtrW(h,-20)&0x80)!=0) return true;
            Native.GetWindowThreadProcessId(h,out uint pid); if(pid==(uint)Environment.ProcessId) return true;
            if(DesktopNative.DwmGetWindowAttribute(h,14,out int cloaked,sizeof(int))==0 && cloaked!=0) return true;
            var title=new StringBuilder(1024); DesktopNative.GetWindowText(h,title,title.Capacity); if(title.Length==0) return true;
            try
            {
                using var process=Process.GetProcessById((int)pid);
                if(process.ProcessName is "explorer" && title.ToString() is "Program Manager") return true;
                if(!DesktopNative.GetWindowRect(h,out var rect)) return true;
                nint monitor=DesktopNative.MonitorFromWindow(h,2);
                var display=monitors.FirstOrDefault(m=>m.Handle==monitor); if(display is null) return true;
                if(rect.Right<=rect.Left || rect.Bottom<=rect.Top) return true;
                result.Add(new(h,pid,process.ProcessName,title.ToString(),display,rect,DesktopNative.IsZoomed(h)));
            }
            catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return true;
        };
        DesktopNative.EnumWindows(callback,0); return result;
    }
    public WindowLayout Capture(string name)
    {
        var windows=Windows().Where(w=>!Native.IsIconic(w.Handle)).Select(w=>
        {
            var r=w.Bounds;
            var placement=new DesktopNative.Placement { Length=(uint)Marshal.SizeOf<DesktopNative.Placement>() };
            if(w.Maximized && DesktopNative.GetWindowPlacement(w.Handle,ref placement))
            { r=placement.Normal; int x=w.Monitor.Work.Left-w.Monitor.Area.Left,y=w.Monitor.Work.Top-w.Monitor.Area.Top; r.Left+=x; r.Right+=x; r.Top+=y; r.Bottom+=y; }
            var m=w.Monitor.Work; double width=m.Right-m.Left,height=m.Bottom-m.Top;
            return new SavedWindow(w.Process,w.Title,w.Monitor.Id,new(Math.Clamp((r.Left-m.Left)/width,0,1),Math.Clamp((r.Top-m.Top)/height,0,1),Math.Clamp((r.Right-r.Left)/width,.01,1),Math.Clamp((r.Bottom-r.Top)/height,.01,1)),w.Maximized,w.Handle.ToInt64(),w.ProcessId);
        }).Take(80).ToList();
        return new(name,windows);
    }
    public string Restore(WindowLayout layout)
    {
        nint foreground=Native.GetForegroundWindow();
        var available=Windows(); var displays=Monitors();
        if(displays.Count==0) return "No displays are available.";
        var used=new HashSet<nint>(); undo.Clear(); int restored=0,missing=0;
        foreach(var saved in layout.Windows)
        {
            var candidates=available.Where(w=>!used.Contains(w.Handle) && w.Process.Equals(saved.Process,StringComparison.OrdinalIgnoreCase)).ToList();
            var window=candidates.FirstOrDefault(w=>saved.SessionHandle!=0 && w.Handle.ToInt64()==saved.SessionHandle && w.ProcessId==saved.SessionProcessId)
                ??candidates.FirstOrDefault(w=>w.Title==saved.Title)??candidates.FirstOrDefault();
            if(window is null) { missing++; continue; }
            used.Add(window.Handle);
            if(!SameProcess(window)) { missing++; continue; }
            var monitor=displays.FirstOrDefault(m=>m.Id==saved.Monitor)??window.Monitor;
            var m=monitor.Work;
            var r=saved.Bounds.OnScreen(m.Left,m.Top,m.Right-m.Left,m.Bottom-m.Top);
            var previous=new DesktopNative.Placement { Length=(uint)Marshal.SizeOf<DesktopNative.Placement>() };
            if(DesktopNative.GetWindowPlacement(window.Handle,ref previous)) undo[window.Handle]=(window,previous);
            Native.ShowWindow(window.Handle,9);
            if(!Native.SetWindowPos(window.Handle,0,r.X,r.Y,r.Width,r.Height,0x14)) { missing++; continue; }
            if(saved.Maximized) Native.ShowWindow(window.Handle,3);
            restored++;
        }
        Native.SetForegroundWindow(foreground);
        return $"Restored {restored} windows. {missing} unavailable. Closed apps were left closed.";
    }
    public string Undo()
    {
        nint foreground=Native.GetForegroundWindow();
        int restored=0;
        foreach(var entry in undo.Values)
        {
            if(!SameProcess(entry.Window)) continue;
            var placement=entry.Placement;
            if(DesktopNative.SetWindowPlacement(entry.Window.Handle,ref placement)) restored++;
        }
        undo.Clear(); Native.SetForegroundWindow(foreground); return $"Restored the previous positions of {restored} windows.";
    }
    public string Pin(OpenWindow window)
    {
        if(!SameProcess(window)) return "This window has closed. Refresh the list.";
        if(pinned.ContainsKey(window.Handle)) return "This window is already pinned by Vela.";
        if(((long)Native.GetWindowLongPtrW(window.Handle,-20)&8)!=0) return "This window is already kept on top by its app.";
        if(!Native.SetWindowPos(window.Handle,-1,0,0,0,0,0x13)) return "Windows did not allow this window to be pinned.";
        pinned[window.Handle]=window; return "Kept above ordinary windows. Unpin here, disable Find a window, or exit Vela to restore its layer.";
    }
    public string Unpin(OpenWindow window)
    {
        if(!pinned.TryGetValue(window.Handle,out var owned) || !SameProcess(owned)) return "Vela has not pinned this window.";
        if(!Native.SetWindowPos(window.Handle,-2,0,0,0,0,0x13)) return "Windows did not allow this window's layer to be restored.";
        pinned.Remove(window.Handle); return "Restored the normal window layer.";
    }
    public void UnpinAll()
    { foreach(var window in pinned.Values.ToArray()) Unpin(window); pinned.Clear(); }
    public void Dispose()=>UnpinAll();
    private static bool SameProcess(OpenWindow window)
    { Native.GetWindowThreadProcessId(window.Handle,out uint pid); return pid==window.ProcessId; }
    public static void Focus(OpenWindow window) { if(!SameProcess(window)) return; if(Native.IsIconic(window.Handle)) Native.ShowWindow(window.Handle,9); Native.SetForegroundWindow(window.Handle); }
}

internal static class DesktopNative
{
    [StructLayout(LayoutKind.Sequential)] internal struct Placement
    { public uint Length,Flags,ShowCommand; public Native.Point MinPosition,MaxPosition; public Native.Rect Normal; }
    [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(nint window,ref Placement placement);
    [DllImport("user32.dll")] internal static extern bool SetWindowPlacement(nint window,ref Placement placement);
    internal delegate bool WindowEnum(nint window,nint data);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct MonitorEx
    { public uint Size; public Native.Rect Monitor,Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device; }
    [DllImport("user32.dll")] internal static extern bool EnumWindows(WindowEnum callback,nint data);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetWindowText(nint window,StringBuilder text,int capacity);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window,out Native.Rect bounds);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(nint window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor,ref MonitorEx info);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint window,uint attribute,out int value,int size);
}
