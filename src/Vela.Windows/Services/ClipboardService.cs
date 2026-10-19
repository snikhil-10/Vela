using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed class ClipboardService(nint hwnd, WindowMessages messages, Func<AppSettings> settings, DispatcherQueue queue) : IDisposable
{
    internal static readonly SemaphoreSlim TransferGate=new(1,1);
    internal static bool TransferBusy=>TransferGate.CurrentCount==0;
    private readonly HashSet<string> active=[];
    private DispatcherQueueTimer? timer;
    private DateTimeOffset deadline;
    private uint sequence;
    private bool handling;
    private int lastDelay;
    private bool plainPasting,disposed;
    private CancellationTokenSource? pastePreparation;
    private DataPackage? pasteOriginal;
    private uint pasteSequence;
    public event Action<string>? Error;
    public event Action<string>? PasteFailed;

    public IFeatureService Feature(string id) => new DelegateFeature(id, () => Enable(id), () => Disable(id));
    private void Enable(string id)
    {
        if(active.Count==0)
        {
            Native.AddClipboardFormatListener(hwnd);
            messages.Message+=Receive;
            timer=queue.CreateTimer(); timer.Interval=TimeSpan.FromSeconds(1); timer.Tick+=Tick; timer.Start();
        }
        active.Add(id); ResetDeadline();
    }
    private void Disable(string id) { active.Remove(id); if(active.Count==0) Detach(); }
    private void ResetDeadline() { sequence=Native.GetClipboardSequenceNumber(); lastDelay=settings().ClipboardClearSeconds; deadline=DateTimeOffset.UtcNow.AddSeconds(lastDelay); }
    public void OptionsChanged()
    {
        if(!settings().EnabledFeatures.Contains("pastePlain")) CancelPaste();
        if(active.Count>0 && lastDelay!=settings().ClipboardClearSeconds) ResetDeadline();
    }
    internal void CancelPaste()=>pastePreparation?.Cancel();
    private void Tick(DispatcherQueueTimer sender, object args)
    {
        if(active.Contains("clipboardGuard") && DateTimeOffset.UtcNow>=deadline)
        {
            if(!TransferBusy && sequence==Native.GetClipboardSequenceNumber()) Clear();
            ResetDeadline();
        }
    }
    private void Receive(uint msg, nuint wp, nint lp)
    {
        if(msg==Native.WM_CLIPBOARDUPDATE) { ResetDeadline(); if(active.Contains("urlCleaner") && settings().AutoCleanLinks) CleanAutomatically(); }
        if(active.Contains("clipboardGuard") && (msg==Native.WM_WTSSESSION_CHANGE && wp==7 && settings().ClearOnLock
            || msg==Native.WM_POWERBROADCAST && wp==4 && settings().ClearOnSleep)) Clear();
    }
    public static async Task<string> ReadText()
    {
        var content=Clipboard.GetContent();
        return content.Contains(StandardDataFormats.Text) ? await content.GetTextAsync() : "";
    }
    public static void WriteText(string text)
    {
        var package=new DataPackage(); package.SetText(text);
        WritePackage(package);
    }
    public static void WritePackage(DataPackage package)
    {
        if(!Clipboard.SetContentWithOptions(package, new ClipboardContentOptions { IsAllowedInHistory=false, IsRoamable=false }))
            throw new InvalidOperationException("The clipboard is busy. Try copying again.");
        Clipboard.Flush();
    }
    private async void CleanAutomatically()
    {
        if(handling || TransferBusy) return;
        handling=true;
        try
        {
            uint readSequence=Native.GetClipboardSequenceNumber();
            var content=Clipboard.GetContent();
            // Preserve rich content, file lists, and images, even if they include a text representation.
            if(content.AvailableFormats.Any(f=>f!=StandardDataFormats.Text)) return;
            string original=await content.GetTextAsync(); string clean=LinkCleaner.Clean(original);
            if(clean!=original && active.Contains("urlCleaner") && settings().AutoCleanLinks && readSequence==Native.GetClipboardSequenceNumber()) WriteText(clean);
        }
        catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { Error?.Invoke("The clipboard is busy. Try again."); }
        finally { handling=false; }
    }
    private void Clear() { try { Clipboard.Clear(); } catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { Error?.Invoke("Windows could not clear the busy clipboard."); } }
    public async Task PastePlain()
    {
        if(disposed || plainPasting || !settings().EnabledFeatures.Contains("pastePlain")) return;
        nint target=Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(target,out uint targetProcess);
        if(target==0 || targetProcess==(uint)Environment.ProcessId) { PasteFailed?.Invoke("Press the plain-paste shortcut while the destination app is focused."); return; }
        if(!TransferGate.Wait(0)) { PasteFailed?.Invoke("A clipboard transfer is finishing. Try again in a moment."); return; }
        plainPasting=true;
        uint temporarySequence=0;
        ClipboardSnapshot? saved=null;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        pastePreparation=timeout;
        try
        {
            uint originalSequence=Native.GetClipboardSequenceNumber();
            // Plain paste and snippets share the same bounded, all-format snapshot.
            saved=await ClipboardPreparation.Read(originalSequence,Native.GetClipboardSequenceNumber,
                ()=>ClipboardSnapshot.Capture(Clipboard.GetContent(),timeout.Token),timeout.Token);
            if(!saved.Package.GetView().Contains(StandardDataFormats.Text)) { PasteFailed?.Invoke("The clipboard does not contain text."); return; }
            string text=saved.Text;
            // Let physical shortcut modifiers go before injecting Ctrl+V.
            for(int i=0;i<100 && ModifiersHeld();i++) await Task.Delay(10,timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if(disposed || ModifiersHeld() || !settings().EnabledFeatures.Contains("pastePlain") || originalSequence!=Native.GetClipboardSequenceNumber() || Native.GetForegroundWindow()!=target) return;
            var publication=await TextClipboardPublisher.Publish(hwnd,originalSequence,text,timeout.Token);
            if(publication is null) return;
            temporarySequence=publication.Sequence; pasteSequence=temporarySequence; pasteOriginal=saved.Package;
            if(publication.Error is not null) throw publication.Error;
            if(!InputService.SendShortcut(new(2,0x56))) throw new InvalidOperationException("Windows declined the paste. Use the destination app's paste command instead.");
            // Once the chord is submitted, keep its payload available while the
            // destination reads it, even if Exit or feature disable is requested.
            await Task.Delay(800);
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { PasteFailed?.Invoke("Plain paste: "+ex.Message); }
        finally
        {
            // Never overwrite a newer copy or a clear performed when the session locks.
            try
            {
                if(saved is not null && temporarySequence!=0) await saved.Restore(temporarySequence);
            }
            catch(Exception ex) { PasteFailed?.Invoke("The original clipboard could not be restored: "+ex.Message); }
            pasteOriginal=null; pasteSequence=0; saved?.Dispose();
            if(ReferenceEquals(pastePreparation,timeout)) pastePreparation=null;
            plainPasting=false; TransferGate.Release(); ResetDeadline();
        }
    }
    private static bool ModifiersHeld()=>new[]{0x10,0x11,0x12,0x5B,0x5C}.Any(k=>(Native.GetAsyncKeyState(k)&0x8000)!=0);
    private void Detach()
    {
        messages.Message-=Receive; Native.RemoveClipboardFormatListener(hwnd);
        timer?.Stop(); if(timer is not null) timer.Tick-=Tick; timer=null;
    }
    public void Dispose()
    {
        disposed=true; CancelPaste();
        // A clean exit during the delay must restore the clipboard before the dispatcher stops.
        try
        {
            if(pasteOriginal is not null && pasteSequence!=0 && pasteSequence==Native.GetClipboardSequenceNumber())
            {
                if(!Clipboard.SetContentWithOptions(pasteOriginal,new ClipboardContentOptions { IsAllowedInHistory=false,IsRoamable=false }))
                    throw new InvalidOperationException("Windows kept the clipboard busy during exit.");
                Clipboard.Flush();
            }
        }
        catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { Error?.Invoke("The clipboard could not be restored while exiting."); }
        active.Clear(); Detach();
    }
}

internal sealed class DelegateFeature(string id, Action start, Action stop) : IFeatureService
{
    private bool started;
    public string Id=>id;
    public void Start() { if(started) return; started=true; start(); }
    public void Stop() { if(!started) return; started=false; stop(); }
    public void Dispose()=>Stop();
}
