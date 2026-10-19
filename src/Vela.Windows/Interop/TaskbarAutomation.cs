using System.Runtime.InteropServices;
using Vela.Core;

namespace Vela.Windows.Interop;

internal static class TaskbarAutomation
{
    internal static List<Native.Rect> VelaButtons(IReadOnlyList<TaskbarTargets.Bar> bars,Action<string> trace)
    {
        var result=new List<Native.Rect>();
        IAutomation? automation=null; ICondition? condition=null;
        try
        {
            automation=(IAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("ff48dba4-60ef-4201-aa87-54103eef594e"),true)!)!;
            automation.CreateTrueCondition(out condition);
            foreach(var bar in bars)
            {
                IElement? root=null; IElements? children=null;
                try
                {
                    // Search only the taskbar, never desktop/application subtrees.
                    // This also works while our drop window covers the button.
                    automation.ElementFromHandle(bar.Window,out root);
                    root.FindAll(4,condition,out children); // TreeScope_Descendants
                    children.GetLength(out int count);
                    for(int i=0;i<Math.Min(count,512);i++)
                    {
                        children.GetElement(i,out var element);
                        try
                        {
                            element.GetCurrentPropertyValue(30005,out object value);
                            if(!TaskbarAppLabel.IsVela(value as string)) continue;
                            element.GetCurrentPropertyValue(30001,out object bounds);
                            trace($"UIA taskbar match name={value} bounds={Format(bounds)}");
                            if(Bounds(bounds) is {} rect && TaskbarTargets.ButtonBounds(rect,bar.Bounds,new((rect.Left+rect.Right)/2,(rect.Top+rect.Bottom)/2)))
                                result.Add(rect);
                        }
                        catch(COMException error) { trace($"UIA button HRESULT=0x{error.HResult:X8}"); }
                        finally { Marshal.ReleaseComObject(element); }
                    }
                }
                catch(COMException error) { trace($"UIA taskbar HRESULT=0x{error.HResult:X8}"); }
                finally
                {
                    if(children is not null) Marshal.ReleaseComObject(children);
                    if(root is not null) Marshal.ReleaseComObject(root);
                }
            }
            trace("Located Vela taskbar buttons="+result.Count);
        }
        catch(COMException error) { trace($"UIA setup HRESULT=0x{error.HResult:X8}"); }
        finally
        {
            if(condition is not null) Marshal.ReleaseComObject(condition);
            if(automation is not null) Marshal.ReleaseComObject(automation);
        }
        return result;
    }
    // Only the element at a known taskbar point and its ancestors are queried.
    // Run on an MTA worker; a shell provider must never block Vela's UI thread.
    internal static Native.Rect? VelaButton(Native.Point point,Native.Rect bar,Action<string> trace)
    {
        IAutomation? automation=null; IElement? element=null; IWalker? walker=null;
        try
        {
            automation=(IAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("ff48dba4-60ef-4201-aa87-54103eef594e"),true)!)!;
            automation.ElementFromPoint(point,out element);
            automation.GetRawViewWalker(out walker);
            for(int depth=0;element is not null && depth<6;depth++)
            {
                element.GetCurrentPropertyValue(30005,out object value); // UIA_NamePropertyId
                string? name=value as string;
                element.GetCurrentPropertyValue(30001,out object bounds); // UIA_BoundingRectanglePropertyId
                trace($"UIA depth={depth} name={name} bounds={Format(bounds)}");
                if(TaskbarAppLabel.IsVela(name) && Bounds(bounds) is {} rect)
                {
                    if(TaskbarTargets.ButtonBounds(rect,bar,point)) return rect;
                }
                walker.GetParentElement(element,out var parent);
                Marshal.ReleaseComObject(element); element=parent;
            }
        }
        catch(COMException error) { trace($"UIA HRESULT=0x{error.HResult:X8}"); }
        finally
        {
            if(element is not null) Marshal.ReleaseComObject(element);
            if(walker is not null) Marshal.ReleaseComObject(walker);
            if(automation is not null) Marshal.ReleaseComObject(automation);
        }
        return null;
    }
    private static Native.Rect? Bounds(object bounds)
    {
        if(bounds is not double[] { Length:4 } r || !r.All(double.IsFinite)
            || r[2]<=0 || r[3]<=0 || r[2]>=600 || r[3]>=300
            || r[0]<int.MinValue || r[1]<int.MinValue || r[0]+r[2]>int.MaxValue || r[1]+r[3]>int.MaxValue) return null;
        return new Native.Rect { Left=(int)Math.Floor(r[0]),Top=(int)Math.Floor(r[1]),Right=(int)Math.Ceiling(r[0]+r[2]),Bottom=(int)Math.Ceiling(r[1]+r[3]) };
    }
    private static string Format(object bounds) => bounds is double[] r ? string.Join(',',r) : bounds?.GetType().Name??"missing";

    // Prefixes of the Windows SDK IUnknown interfaces; order is ABI order from
    // UIAutomationClient.h. Unused prefix methods retain their original slots.
    [ComImport,Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAutomation
    {
        void CompareElements(nint first,nint second,out int same);
        void CompareRuntimeIds(nint first,nint second,out int same);
        void GetRootElement(out IElement element);
        void ElementFromHandle(nint window,out IElement element);
        void ElementFromPoint(Native.Point point,out IElement element);
        void GetFocusedElement(out IElement element);
        void GetRootElementBuildCache(nint cache,out IElement element);
        void ElementFromHandleBuildCache(nint window,nint cache,out IElement element);
        void ElementFromPointBuildCache(Native.Point point,nint cache,out IElement element);
        void GetFocusedElementBuildCache(nint cache,out IElement element);
        void CreateTreeWalker(nint condition,out IWalker walker);
        void GetControlViewWalker(out IWalker walker);
        void GetContentViewWalker(out IWalker walker);
        void GetRawViewWalker(out IWalker walker);
        void GetRawViewCondition(out ICondition condition);
        void GetControlViewCondition(out ICondition condition);
        void GetContentViewCondition(out ICondition condition);
        void CreateCacheRequest(out nint cache);
        void CreateTrueCondition(out ICondition condition);
    }
    [ComImport,Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IElement
    {
        void SetFocus();
        void GetRuntimeId(out nint runtimeId);
        void FindFirst(int scope,nint condition,out IElement element);
        void FindAll(int scope,ICondition condition,out IElements elements);
        void FindFirstBuildCache(int scope,nint condition,nint cache,out IElement element);
        void FindAllBuildCache(int scope,nint condition,nint cache,out nint elements);
        void BuildUpdatedCache(nint cache,out IElement element);
        void GetCurrentPropertyValue(int property,[MarshalAs(UnmanagedType.Struct)] out object value);
    }
    [ComImport,Guid("352ffba8-0973-437c-a61f-f64cafd81df9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICondition { }
    [ComImport,Guid("14314595-b4bc-4055-95f2-58f2e42c9855"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IElements
    {
        void GetLength(out int length);
        void GetElement(int index,out IElement element);
    }
    [ComImport,Guid("4042c624-389c-4afc-a630-9df854a541fc"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWalker
    {
        void GetParentElement(IElement element,out IElement? parent);
    }
}
