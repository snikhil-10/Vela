using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Vela.Core;
using Vela.Windows.Interop;
using Vela.Windows.UI;

namespace Vela.Windows.Services;

internal sealed class ShelfDropService(AppController controller,DispatcherQueue queue,Func<Native.Rect?> trayBounds) : IFeatureService
{
    public string Id=>"shelf";
    private DispatcherQueueTimer? timer;
    private ShelfDropWindow? target;
    private readonly ShelfDragGesture gesture=new();
    private Native.Rect? shown;
    private List<Native.Rect> bars=[], buttons=[];
    private long refreshAt, releaseAt, buttonsAt;
    private bool started, resolving, dragging;
    private int generation;
    private readonly HashSet<string> diagnosticLines=[];
    private readonly bool diagnostics=Environment.GetEnvironmentVariable("VELA_SHELF_DIAGNOSTICS")=="1";
    public void Start()
    {
        started=true; timer=queue.CreateTimer(); timer.Interval=TimeSpan.FromMilliseconds(30); timer.Tick+=Tick;
        OptionsChanged();
    }
    internal void OptionsChanged()
    {
        if(!started) return;
        generation++; Hide(true); gesture.Reset(); dragging=false; buttons=[]; bars=[]; refreshAt=0;
        WriteDiagnostics(["Started target="+controller.Settings.ShelfDropTarget]);
        controller.KeepShelfTaskbarIcon(controller.Settings.ShelfDropTarget=="velaIcon");
        if(controller.Settings.ShelfDropTarget!="manual" && target is null) { target=new(controller); controller.Track(target); }
        if(controller.Settings.ShelfDropTarget=="manual") timer?.Stop();
        else { timer?.Start(); RefreshTargets(); }
    }
    private void Hide(bool immediately=false,int dropGraceMilliseconds=0)
    {
        if(target is not null && !target.Receiving)
        { if(immediately) target.HideImmediately(); else _=target.DismissAsync(dropGraceMilliseconds); }
        shown=null;
    }
    private async void RefreshTargets()
    {
        if(resolving || !started || controller.Settings.ShelfDropTarget=="manual") return;
        resolving=true;
        int version=generation;
        bool icon=controller.Settings.ShelfDropTarget=="velaIcon";
        var messages=new List<string>();
        try
        {
            var result=await Task.Run(()=>
            {
                var windows=TaskbarTargets.BarWindows();
                var found=icon ? TaskbarAutomation.VelaButtons(windows,messages.Add) : [];
                return (Bars:windows.Select(bar=>bar.Bounds).ToList(),Buttons:found);
            });
            if(!started || version!=generation) return;
            bars=result.Bars; buttons=result.Buttons; buttonsAt=Environment.TickCount64;
            WriteDiagnostics(["Taskbars found="+bars.Count]); WriteDiagnostics(messages);
        }
        catch(Exception error)
        { WriteDiagnostics(["Taskbar discovery failed: "+error.Message]); buttons=[]; Hide(); }
        finally
        {
            resolving=false;
            refreshAt=version==generation ? Environment.TickCount64+(dragging?200:1000) : 0;
        }
    }
    private void Tick(DispatcherQueueTimer sender,object args)
    {
        try
        {
            if(target?.Receiving==true) return;
            if(Environment.TickCount64>=refreshAt) RefreshTargets();
            bool left=(Native.GetAsyncKeyState(1)&0x8000)!=0;
            if(!controller.ShelfEnabled || controller.Input.Cleaning) { gesture.Reset(); dragging=false; Hide(true); return; }
            // OLE may dispatch Drop just after the mouse-button state changes.
            if(!left) { gesture.Reset(); dragging=false; Hide(false,(int)Math.Max(0,releaseAt-Environment.TickCount64)); return; }
            releaseAt=Environment.TickCount64+250;
            var gui=new Native.GuiThreadInfo { Size=(uint)Marshal.SizeOf<Native.GuiThreadInfo>() };
            Native.GetCursorPos(out var point);
            bool moving=Native.GetGUIThreadInfo(0,ref gui) && (gui.Flags&2)!=0;
            if(!gesture.Update(true,point.X,point.Y,bars.Any(r=>TaskbarTargets.Contains(r,point)),moving,(Native.GetAsyncKeyState(0x1B)&0x8000)!=0))
            { dragging=false; Hide(); return; }
            if(!dragging) { dragging=true; refreshAt=0; }
            Native.Rect? rect=null;
            string mode=controller.Settings.ShelfDropTarget;
            if(mode=="tray") rect=trayBounds();
            else if(mode=="velaIcon")
            {
                if(Environment.TickCount64-buttonsAt>2000) { Hide(); return; }
                // The button is discovered before the pointer arrives. The overlay
                // receives the drop on the icon and prevents Explorer's hover-open.
                var monitor=Native.MonitorFromPoint(point,2);
                rect=buttons.Select(b=>(Native.Rect?)b).FirstOrDefault(r=>r is {} b && Native.MonitorFromPoint(new((b.Left+b.Right)/2,(b.Top+b.Bottom)/2),2)==monitor)
                    ??buttons.Select(b=>(Native.Rect?)b).FirstOrDefault();
            }
            else if(bars.FirstOrDefault(r=>point.X>=r.Left && point.X<r.Right && point.Y>=r.Top-220 && point.Y<r.Bottom+220) is var bar && bar.Right>bar.Left)
                rect=bar;
            // Even the notification icon must be on a visible taskbar, rather
            // than an overflow popup, so every animation stays inside the bar.
            if(rect is null || !bars.Any(bar=>rect.Value.Left>=bar.Left && rect.Value.Top>=bar.Top
                && rect.Value.Right<=bar.Right && rect.Value.Bottom<=bar.Bottom)) { Hide(); return; }
            if(shown is {} existing && Same(existing,rect.Value)) return;
            if(target is null) { target=new(controller); controller.Track(target); }
            target.Place(rect.Value,mode); shown=rect;
            WriteDiagnostics(["Drop surface shown",target.FrameDiagnostics]);
        }
        catch(Exception error) { Hide(); timer?.Stop(); controller.Report("Shelf drop target stopped: "+error.Message); }
    }
    private static bool Same(Native.Rect a,Native.Rect b)=>a.Left==b.Left && a.Top==b.Top && a.Right==b.Right && a.Bottom==b.Bottom;
    private void WriteDiagnostics(IEnumerable<string> messages)
    {
        if(!diagnostics) return;
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Vela","shelf-target.log");
        foreach(string message in messages)
        {
            if(diagnosticLines.Count>=64) break;
            string line=message.Replace('\r',' ').Replace('\n',' ');
            if(line.Length>240) line=line[..240];
            if(!diagnosticLines.Add(line)) continue;
            try { File.AppendAllText(path,line+Environment.NewLine); }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
        }
    }
    public void Stop()
    {
        started=false; generation++; timer?.Stop();
        if(timer is not null) timer.Tick-=Tick;
        timer=null; target?.HideImmediately(); target?.Close(); target=null; gesture.Reset(); shown=null; dragging=false; buttons=[]; bars=[];
        controller.KeepShelfTaskbarIcon(false);
    }
    public void Dispose()=>Stop();
}
