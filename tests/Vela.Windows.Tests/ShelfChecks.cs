using Vela.Windows.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using WinRT;

internal static class ShelfChecks
{
    private static void Require(bool value,string message)
    { if(!value) throw new InvalidOperationException(message); }

    internal static async Task Search(StorageFolder folder)
    {
        var shelf=new ShelfService();
        var text=new DataPackage(); text.SetText("Release notes 東京\nBuild 42: exact quoted phrase");
        await shelf.Add(text.GetView());
        var link=new DataPackage(); link.SetWebLink(new Uri("https://example.test/releases?q=notes"));
        await shelf.Add(link.GetView());
        var files=await folder.CreateFolderAsync("Shelf reports 東京");
        var one=await files.CreateFileAsync("April report.txt"); await FileIO.WriteTextAsync(one,"original first");
        var two=await files.CreateFileAsync("May report.txt"); await FileIO.WriteTextAsync(two,"original second");
        var package=new DataPackage(); package.SetStorageItems([one,two]); await shelf.Add(package.GetView());
        var original=shelf.Items.ToArray();
        Require(shelf.Find(null).SequenceEqual(original) && shelf.Find("  \"").SequenceEqual(original),"An empty query changed shelf order or identity.");
        Require(shelf.Find("東京 build").Single()==original[0],"Unicode or multi-word text search failed.");
        Require(shelf.Find("\"exact quoted\" 42").Single()==original[0],"Quoted phrase search failed.");
        Require(shelf.Find("\"notes Build\"").Count==0,"A quoted phrase crossed a newline.");
        Require(shelf.Find("EXAMPLE.TEST notes").Single()==original[1],"Case-insensitive link search failed.");
        Require(shelf.Find("\"Shelf reports 東京\" REPORT.TXT").SequenceEqual(original.Skip(2)),"File path search changed reference identity or transfer order.");
        Require(shelf.Find("no-such-shelf-entry").Count==0 && shelf.Items.SequenceEqual(original),"An empty result removed hidden entries.");
        var visible=shelf.Find("May report");
        var transferred=await ShelfService.Package(visible).GetView().GetStorageItemsAsync();
        Require(transferred.Count==1 && transferred[0].Path==two.Path,"A filtered transfer included a hidden file.");
        shelf.CompleteDrag(visible,DataPackageOperation.None,true);
        Require(shelf.Items.SequenceEqual(original),"Canceled filtered drag removed entries.");
        shelf.CompleteDrag(visible,DataPackageOperation.Copy,true,copyConfirmed:true);
        Require(shelf.Items.Count==3 && shelf.Items.Contains(original[2]) && !shelf.Items.Contains(original[3]),"Confirmed filtered drag removed hidden entries.");
        var added=new DataPackage(); added.SetText("new report"); await shelf.Add(added.GetView());
        Require(shelf.Find("report").Count==2 && shelf.Find("").Count==4,"Search did not reflect additions and removals.");
        Require(await FileIO.ReadTextAsync(one)=="original first" && await FileIO.ReadTextAsync(two)=="original second","Search or filtered transfer edited source files.");
        Console.WriteLine("INFO Shelf search: Unicode, quoted phrases, links and file paths; hidden originals retained; filtered file package and exact-entry removal verified.");
    }

    internal static async Task Cancellation()
    {
        var shelf=new ShelfService();
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var data=new DataPackage();
        data.SetDataProvider(StandardDataFormats.Text,request=>
        {
            var deferral=request.GetDeferral(); ready.SetResult();
            _=Complete();
            async Task Complete()
            {
                try { await release.Task; request.SetData("late fixture"); }
                finally { deferral.Complete(); finished.SetResult(); }
            }
        });
        var pending=shelf.Add(data.GetView());
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            shelf.CancelPendingAdds(); release.TrySetResult();
            bool canceled=false;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); } catch(OperationCanceledException) { canceled=true; }
            Require(canceled && shelf.Items.Count==0,"Disabled shelf accepted an in-flight provider.");
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var next=new DataPackage(); next.SetText("new session");
            Require(await shelf.Add(next.GetView())==1 && shelf.Items.Single().Text=="new session","Cancellation poisoned later additions.");
            using var canceledToken=new CancellationTokenSource(); canceledToken.Cancel();
            canceled=false;
            try { await shelf.Add(next.GetView(),canceledToken.Token); } catch(OperationCanceledException) { canceled=true; }
            Require(canceled && shelf.Items.Count==1,"A closed tool accepted a canceled addition.");
        }
        finally { release.TrySetResult(); }
    }

    internal static async Task DragIdentity()
    {
        var shelf=new ShelfService(); var data=new DataPackage(); data.SetText("same fixture");
        await shelf.Add(data.GetView()); var original=shelf.Items.Single();
        shelf.Items.Remove(original); await shelf.Add(data.GetView()); var replacement=shelf.Items.Single();
        shelf.CompleteDrag([original],DataPackageOperation.Copy,true);
        Require(shelf.Items.Count==1 && ReferenceEquals(shelf.Items[0],replacement),"Late drag completion removed a newly added identical entry.");
        foreach(var result in new[]{DataPackageOperation.None,DataPackageOperation.Move,DataPackageOperation.Link,DataPackageOperation.Copy|DataPackageOperation.Move})
            shelf.CompleteDrag([replacement],result,true);
        Require(shelf.Items.Count==1,"An unoffered transfer operation removed a shelf entry.");
        shelf.CompleteDrag([replacement],DataPackageOperation.Copy,false);
        Require(shelf.Items.Count==1,"Disabled removal discarded a shelf entry.");
        shelf.CompleteDrag([replacement],DataPackageOperation.Copy,true);
        Require(shelf.Items.Count==0,"An accepted copy did not remove its exact entry.");
    }
    internal static async Task FileCompletion(StorageFolder folder)
    {
        var one=await folder.CreateFileAsync("receipt-one.txt"); await FileIO.WriteTextAsync(one,"receipt original one");
        var two=await folder.CreateFileAsync("receipt-two.txt"); await FileIO.WriteTextAsync(two,"receipt original two");
        DataPackage Package()
        { return ShelfService.Package([new(one.Name,null,one),new(two.Name,null,two)]); }
        var package=Package(); var shelf=new ShelfService(); await shelf.Add(package.GetView()); var dragged=shelf.Items.ToArray();
        Require(dragged.All(item=>(item.File!.Attributes&global::Windows.Storage.FileAttributes.ReadOnly)==0),"Writable shelf files acquired read-only storage access in a WinRT round trip.");
        var external=new DataPackage(); external.SetStorageItems([one,two]);
        var imported=new ShelfService(); await imported.Add(external.GetView());
        var exported=await ShelfService.Package(imported.Items.ToArray()).GetView().GetStorageItemsAsync();
        Require(exported.All(item=>(item.Attributes&global::Windows.Storage.FileAttributes.ReadOnly)==0),"Read-only incoming storage proxies made writable originals read-only on re-export.");
        shelf.CompleteDrag(dragged,DataPackageOperation.Copy,true);
        Require(shelf.Items.Count==2,"An accepted drop without completion removed file entries.");
        using var feedback=ShellDropFeedback.Capture(package,[one,two]);
        Require(feedback is not null,"Native tracked DataPackage IDataObject bridge is unavailable.");
        package.As<ShellDropFeedback.IDataObjectProvider>().GetDataObject(out var target);
        var hdrop=new FORMATETC { cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL };
        Require(target.QueryGetData(ref hdrop)<0,"A streamed file transfer exposed the unconfirmed filesystem-copy route.");
        var descriptors=DescriptorAttributes(target);
        Require(descriptors.Length==2 && descriptors.All(value=>(value&0x101)==0),"Writable source copies became read-only or temporary.");
        var originalAttributes=File.GetAttributes(one.Path);
        try
        {
            File.SetAttributes(one.Path,originalAttributes|System.IO.FileAttributes.ReadOnly);
            var protectedPackage=Package(); using var protectedFeedback=ShellDropFeedback.Capture(protectedPackage,[one,two]);
            protectedPackage.As<ShellDropFeedback.IDataObjectProvider>().GetDataObject(out var protectedData);
            Require((DescriptorAttributes(protectedData)[0]&1)!=0,"A read-only original lost its attribute in streamed transfer metadata.");
        }
        finally { File.SetAttributes(one.Path,originalAttributes); }
        var asynchronous=target as IShelfTransferAsync;
        Require(asynchronous is not null,"The outgoing data object does not expose async completion.");
        var roundTrip=await package.GetView().GetStorageItemsAsync();
        Require(roundTrip.Count==2 && roundTrip[0].Path==one.Path && roundTrip[1].Path==two.Path,"Wrapping transfer feedback changed its file payload/order.");
        Receipt(target,(int)DataPackageOperation.Copy);
        Require(feedback!.Read() is null,"A logical Copy effect was mistaken for completed file work.");
        asynchronous!.GetAsyncMode(out bool supportsAsync);
        Require(supportsAsync,"The source did not offer asynchronous file extraction.");
        asynchronous.StartOperation(0); asynchronous.InOperation(out bool active);
        Require(active && feedback.Read() is null,"A started operation was already marked successful.");
        var awaiting=feedback.ConfirmCopy(default);
        Require(!awaiting.IsCompleted,"An early Copy effect bypassed the completion callback.");
        asynchronous.EndOperation(unchecked((int)0x800704C7),0,(uint)DataPackageOperation.Copy);
        Require(!await awaiting && feedback.Read()==DataPackageOperation.None,"Cancel after a logical Copy removed entries.");
        shelf.CompleteDrag(dragged,DataPackageOperation.Copy,true,copyConfirmed:false);
        Require(shelf.Items.Count==2,"Canceled file extraction removed entries.");

        foreach(var (status,effect,starts,asyncMode) in new[]{
            (unchecked((int)0x80004005),DataPackageOperation.Copy,1,true),
            (1,DataPackageOperation.Copy,1,true),
            (0,DataPackageOperation.None,1,true),
            (0,DataPackageOperation.Move,1,true),
            (0,DataPackageOperation.Link,1,true),
            (0,DataPackageOperation.Copy|DataPackageOperation.Move,1,true),
            (0,DataPackageOperation.Copy,0,true),
            (0,DataPackageOperation.Copy,2,true),
            (0,DataPackageOperation.Copy,1,false)})
        {
            var failedPackage=Package();
            using var rejected=ShellDropFeedback.Capture(failedPackage,[one,two]);
            Require(rejected is not null,"Failed to prepare a completion-case fixture.");
            failedPackage.As<ShellDropFeedback.IDataObjectProvider>().GetDataObject(out var failedTarget);
            var operation=(IShelfTransferAsync)failedTarget; operation.SetAsyncMode(asyncMode);
            for(int index=0;index<starts;index++) operation.StartOperation(0);
            Receipt(failedTarget,(int)DataPackageOperation.Copy);
            operation.EndOperation(status,0,(uint)effect);
            Require(!await rejected!.ConfirmCopy(default),"Failed, partial, unoffered or invalid completion confirmed Copy.");
        }
        using(var cancel=new CancellationTokenSource())
        {
            var waitingPackage=Package(); using var waiting=ShellDropFeedback.Capture(waitingPackage,[one,two]);
            cancel.Cancel(); bool stopped=false;
            try { await waiting!.ConfirmCopy(cancel.Token); } catch(OperationCanceledException) { stopped=true; }
            Require(stopped && shelf.Items.Count==2,"Cancellation did not retain entries.");
        }
        var success=Package(); using var accepted=ShellDropFeedback.Capture(success,[one,two]);
        success.As<ShellDropFeedback.IDataObjectProvider>().GetDataObject(out var successTarget);
        var successful=(IShelfTransferAsync)successTarget;
        successful.StartOperation(0); successful.EndOperation(0,0,(uint)DataPackageOperation.Copy);
        Require(await accepted!.ConfirmCopy(default),"An explicit successful completed Copy was rejected.");
        shelf.CompleteDrag(dragged,DataPackageOperation.Copy,false,copyConfirmed:true);
        Require(shelf.Items.Count==2,"Disabled removal discarded confirmed entries.");
        shelf.Items.Remove(dragged[0]); await shelf.Add(Package().GetView()); var replacement=shelf.Items.Last();
        shelf.CompleteDrag(dragged,DataPackageOperation.Copy,true,copyConfirmed:true);
        Require(shelf.Items.Count==2 && shelf.Items.Contains(replacement),"Late confirmation removed newly imported entries.");
        Require(await FileIO.ReadTextAsync(one)=="receipt original one" && await FileIO.ReadTextAsync(two)=="receipt original two","Transfer feedback modified original files.");
        Console.WriteLine("INFO Native async transfer: early logical Copy is ignored; canceled/failed/partial/invalid completion retains entries; only S_OK Copy after one async start confirms exact originals; two-file payload/order unchanged.");
    }
    private static void Receipt(IDataObject target,int effect)
    {
        var format=new FORMATETC { cfFormat=unchecked((short)RegisterClipboardFormatW(ShellDropFeedback.LogicalEffect)),dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL };
        nint memory=GlobalAlloc(2,4); if(memory==0) throw new OutOfMemoryException();
        try
        {
            nint pointer=GlobalLock(memory); if(pointer==0) throw new InvalidOperationException("Cannot lock receipt fixture.");
            try { Marshal.WriteInt32(pointer,effect); } finally { GlobalUnlock(memory); }
            var medium=new STGMEDIUM { tymed=TYMED.TYMED_HGLOBAL,unionmember=memory };
            target.SetData(ref format,ref medium,true); memory=0;
        }
        finally { if(memory!=0) GlobalFree(memory); }
    }
    private static uint[] DescriptorAttributes(IDataObject data)
    {
        var format=new FORMATETC { cfFormat=unchecked((short)RegisterClipboardFormatW("FileGroupDescriptorW")),dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL };
        data.GetData(ref format,out var medium);
        try
        {
            Require(medium.tymed==TYMED.TYMED_HGLOBAL,"File descriptors did not use HGLOBAL.");
            nint pointer=GlobalLock(medium.unionmember); Require(pointer!=0,"Could not read native file descriptors.");
            try
            {
                int count=Marshal.ReadInt32(pointer); Require(count is >0 and <=256,"Unexpected native descriptor count.");
                return Enumerable.Range(0,count).Select(index=>unchecked((uint)Marshal.ReadInt32(pointer,4+index*592+36))).ToArray();
            }
            finally { GlobalUnlock(medium.unionmember); }
        }
        finally { ReleaseStgMedium(ref medium); }
    }
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags,nuint size);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint memory);

}
