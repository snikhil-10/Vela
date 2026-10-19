using System.Runtime.InteropServices;
using System.Text;
using Vela.Core;

namespace Vela.Windows.Interop;

internal static class TaskbarTargets
{
    internal readonly record struct Bar(nint Window,Native.Rect Bounds);
    internal static bool Contains(Native.Rect rect,Native.Point point) => point.X>=rect.Left && point.X<rect.Right && point.Y>=rect.Top && point.Y<rect.Bottom;
    internal static List<Bar> BarWindows()
    {
        var result=new List<Bar>();
        EnumWindows((window,_)=>
        {
            var name=new StringBuilder(80); Native.GetClassName(window,name,name.Capacity);
            if(name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" && GetWindowRect(window,out var rect) && IsWindowVisible(window)) result.Add(new(window,rect));
            return true;
        },0);
        return result;
    }
    internal static List<Native.Rect> Bars()=>BarWindows().Select(bar=>bar.Bounds).ToList();
    internal static bool ButtonBounds(Native.Rect rect,Native.Rect bar,Native.Point point) =>
        rect.Right>rect.Left && rect.Bottom>rect.Top && rect.Right-rect.Left<600 && rect.Bottom-rect.Top<300
        && rect.Left>=bar.Left && rect.Top>=bar.Top && rect.Right<=bar.Right && rect.Bottom<=bar.Bottom && Contains(rect,point);
    internal static Native.Rect? VelaButton(Native.Point point,Native.Rect bar,Action<string> trace)
    {
        return TaskbarAutomation.VelaButton(point,bar,trace)??LegacyButton(point,bar,trace);
    }
    private static Native.Rect? LegacyButton(Native.Point point,Native.Rect bar,Action<string> trace)
    {
        // Resolve only the button under the pointer, never enumerate another app's content.
        int result=AccessibleObjectFromPoint(point,out var accessible,out object child);
        if(result!=0 || accessible is null) { trace($"MSAA HRESULT=0x{result:X8}"); return null; }
        try
        {
            for(int depth=0;depth<6;depth++)
            {
                string? name=accessible[child];
                trace($"MSAA depth={depth} name={name}");
                if(TaskbarAppLabel.IsVela(name))
                {
                    accessible.Location(out int x,out int y,out int w,out int h,child);
                    var rect=new Native.Rect { Left=x,Top=y,Right=x+w,Bottom=y+h };
                    if(ButtonBounds(rect,bar,point)) return rect;
                }
                if(accessible.Parent is not IAccessible parent) break;
                Marshal.ReleaseComObject(accessible); accessible=parent; child=0;
            }
        }
        catch(COMException error) { trace($"MSAA HRESULT=0x{error.HResult:X8}"); }
        finally { Marshal.ReleaseComObject(accessible); }
        return null;
    }
    private delegate bool EnumWindow(nint window,nint data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback,nint data);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out Native.Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromPoint(Native.Point point,[MarshalAs(UnmanagedType.Interface)] out IAccessible? accessible,[MarshalAs(UnmanagedType.Struct)] out object child);
    [ComImport,Guid("618736E0-3C3D-11CF-810C-00AA00389B71"),InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    private interface IAccessible
    {
        [DispId(-5000)] object Parent { [return:MarshalAs(UnmanagedType.IDispatch)] get; }
        [DispId(-5003)] string? this[[MarshalAs(UnmanagedType.Struct)] object child] { [return:MarshalAs(UnmanagedType.BStr)] get; }
        [DispId(-5015)] void Location(out int left,out int top,out int width,out int height,[MarshalAs(UnmanagedType.Struct)] object child);
    }
}
