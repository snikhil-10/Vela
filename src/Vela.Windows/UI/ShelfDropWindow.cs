using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Vela.Windows.Interop;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

internal sealed class ShelfDropWindow : Window
{
    private readonly ShelfDropSurface surface;
    private int presentation;
    private bool visible,closed;
    private Task? dismissal;
    internal bool Receiving { get; private set; }
    internal ShelfDropWindow(AppController controller)
    {
        surface=new();
        Content=surface; WindowStyle.Configure(this,240,48,true);
        if(AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(false,false);
        nint handle=WindowStyle.Handle(this);
        ShelfTargetBackdrop.RemoveFrame(handle);
        SystemBackdrop=new ShelfTargetBackdrop(handle);
        uint disableWindowTransitions=1;
        Native.DwmSetWindowAttribute(handle,3,ref disableWindowTransitions,sizeof(uint));
        Closed+=(_,_)=> { closed=true; presentation++; surface.Reset(); };
        surface.DragOver+=(_,e)=>
        {
            if(ShelfService.Accepts(e.DataView) && controller.ShelfEnabled)
            { e.AcceptedOperation=DataPackageOperation.Copy; e.DragUIOverride.IsCaptionVisible=false; }
            else e.AcceptedOperation=DataPackageOperation.None;
            e.Handled=true;
        };
        surface.Drop+=async (_,e)=>
        {
            var deferral=e.GetDeferral(); Receiving=true; bool added=false;
            try
            {
                if(!controller.ShelfEnabled) { e.AcceptedOperation=DataPackageOperation.None; return; }
                await controller.ShelfData.Add(e.DataView); e.AcceptedOperation=DataPackageOperation.Copy;
                added=true;
            }
            catch(Exception ex) { e.AcceptedOperation=DataPackageOperation.None; controller.Report(ex.Message); }
            finally
            {
                e.Handled=true; deferral.Complete();
                // Finish OLE first, then ease the highlight back to Explorer's
                // existing icon before showing the collection window.
                await DismissAsync(); Receiving=false;
                if(added && controller.ShelfEnabled && !closed) DispatcherQueue.TryEnqueue(()=>controller.ShowShelf());
            }
        };
    }
    internal void Place(Native.Rect rect,string mode)
    {
        nint handle=WindowStyle.Handle(this);
        bool appearing=!visible; visible=true; presentation++; dismissal=null;
        int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
        ShelfTargetBackdrop.RemoveFrame(handle);
        // Move first so the scale belongs to the target monitor, including on first show.
        Native.SetWindowPos(handle,-1,rect.Left,rect.Top,width,height,0x0010);
        double scale=Math.Max(1,Native.GetDpiForWindow(handle)/96.0);
        // A taskbar target follows the shell even when Vela has an app-only theme override.
        surface.RequestedTheme=ThemedIcon.ShellIsLight ? ElementTheme.Light : ElementTheme.Dark;
        surface.Fit(mode,width/scale,height/scale);
        if(appearing) _=surface.Transition(true);
        Native.SetWindowPos(handle,-1,rect.Left,rect.Top,width,height,0x0010|0x0040); // topmost, no activation, show
    }
    internal string FrameDiagnostics
    {
        get
        {
            nint handle=WindowStyle.Handle(this);
            return $"Receiver style=0x{(long)Native.GetWindowLongPtrW(handle,-16):X8} extended=0x{(long)Native.GetWindowLongPtrW(handle,-20):X8}";
        }
    }
    internal Task DismissAsync(int dropGraceMilliseconds=0)
    {
        if(closed) return Task.CompletedTask;
        if(!visible) return dismissal??Task.CompletedTask;
        visible=false; int version=++presentation;
        return dismissal=FinishDismissal(version,surface.Transition(false),dropGraceMilliseconds);
    }
    private async Task FinishDismissal(int version,Task fade,int grace)
    {
        await Task.WhenAll(fade,Task.Delay(grace));
        // A new drag can reverse the fade. An older completion must never hide it.
        if(!closed && !visible && version==presentation) AppWindow.Hide();
    }
    internal void HideImmediately()
    {
        if(closed) return;
        visible=false; presentation++; dismissal=null; surface.Reset(); AppWindow.Hide();
    }
}
