using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed class ClipboardSnapshot : IDisposable
{
    internal DataPackage Package { get; }=new();
    internal string Text { get; private set; }="";
    private readonly List<IDisposable> copies=[];
    internal async Task Restore(uint temporarySequence)
    {
        bool empty=Package.GetView().AvailableFormats.Count==0;
        var result=await ClipboardRestoration.Restore(temporarySequence,Native.GetClipboardSequenceNumber,()=>
        {
            if(empty) { Clipboard.Clear(); return true; }
            return Clipboard.SetContentWithOptions(Package,new ClipboardContentOptions { IsAllowedInHistory=false,IsRoamable=false });
        });
        if(result==ClipboardRestoreResult.Busy) throw new InvalidOperationException("Windows kept the clipboard busy; its previous content could not be restored.");
        if(result==ClipboardRestoreResult.Restored && !empty) Clipboard.Flush();
    }
    internal static async Task<ClipboardSnapshot> Capture(DataPackageView original,CancellationToken token)
    {
        var snapshot=new ClipboardSnapshot();
        try
        {
            token.ThrowIfCancellationRequested();
            if(original.AvailableFormats.Count>64) throw new InvalidOperationException("Too many clipboard formats to preserve safely.");
            snapshot.Package.RequestedOperation=original.RequestedOperation;
            ulong streamBytes=0;
            foreach(string format in original.AvailableFormats)
            {
                token.ThrowIfCancellationRequested();
                object data=await original.GetDataAsync(format).AsTask(token);
                if(data is IRandomAccessStreamReference reference)
                {
                    using var input=await reference.OpenReadAsync().AsTask(token);
                    streamBytes+=input.Size;
                    if(input.Size>64*1024*1024 || streamBytes>128*1024*1024) throw new InvalidOperationException("This clipboard item is too large to preserve safely.");
                    var copy=new InMemoryRandomAccessStream(); snapshot.copies.Add(copy);
                    await RandomAccessStream.CopyAsync(input,copy).AsTask(token);
                    copy.Seek(0); data=RandomAccessStreamReference.CreateFromStream(copy);
                }
                snapshot.Package.SetData(format,data);
                if(format==StandardDataFormats.Text && data is string text) snapshot.Text=text;
            }
            return snapshot;
        }
        catch { snapshot.Dispose(); throw; }
    }
    public void Dispose() { foreach(var copy in copies) copy.Dispose(); copies.Clear(); }
}
