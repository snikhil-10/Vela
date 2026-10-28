using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Vela.Windows.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;

internal static class ShellCopyChecks
{
    internal static Task Run(StorageFolder folder)
    {
        var complete=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try { RunSta(Path.Combine(folder.Path,"actual-shell-copy")); complete.SetResult(); }
            catch(Exception error) { complete.SetException(error); }
        }) { IsBackground=true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return complete.Task;
    }
    private static void RunSta(string root)
    {
        int initialized=OleInitialize(0); nint item=0,target=0,dataPointer=0,unknown=0;
        try
        {
            Marshal.ThrowExceptionForHR(initialized);
            string source=Path.Combine(root,"source"),destination=Path.Combine(root,"destination"); Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
            string[] names=["ordinary 東京.txt","empty.txt","read-only.txt"];
            for(int i=0;i<names.Length;i++) File.WriteAllText(Path.Combine(source,names[i]),i==1?"":"Generated Shell bytes "+i);
            File.SetAttributes(Path.Combine(source,names[2]),System.IO.FileAttributes.ReadOnly|System.IO.FileAttributes.Archive);
            var files=names.Select(name=>(IStorageItem)StorageFile.GetFileFromPathAsync(Path.Combine(source,name)).AsTask().GetAwaiter().GetResult()).ToArray();
            byte[][] originals=files.Select(file=>File.ReadAllBytes(file.Path)).ToArray();
            var incoming=new DataPackage(); incoming.SetStorageItems(files);
            var shelf=new ShelfService(); shelf.Add(incoming.GetView()).GetAwaiter().GetResult();
            var imported=shelf.Items.Select(entry=>entry.File!).ToArray();
            var package=ShelfService.Package(shelf.Items.ToArray());
            using var feedback=ShellDropFeedback.Capture(package,imported)??throw new Exception("No tracked source");
            package.As<ShellDropFeedback.IDataObjectProvider>().GetDataObject(out var data);
            unknown=Marshal.GetIUnknownForObject(data); Guid dataIid=new("0000010E-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown,in dataIid,out dataPointer));
            Guid itemIid=new("43826D1E-E718-42EE-BC55-A1E261C37BFE"),handler=new("3981E225-F559-11D3-8E3A-00C04F6837D5"),dropIid=new("00000122-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(destination,0,in itemIid,out item));
            Marshal.ThrowExceptionForHR(Method<Bind>(item,3)(item,0,in handler,in dropIid,out target));
            uint effect=1; Marshal.ThrowExceptionForHR(Method<Drag>(target,3)(target,dataPointer,9,new(0,0),ref effect));
            Require(effect==1,"Actual Shell DragEnter rejected Copy");
            effect=1; Marshal.ThrowExceptionForHR(Method<Drag>(target,6)(target,dataPointer,8,new(0,0),ref effect));
            Require(effect==1,"Actual Shell Drop rejected Copy");
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var confirmation=feedback.ConfirmCopy(timeout.Token); using var done=new EventWaitHandle(false,EventResetMode.ManualReset);
            _=confirmation.ContinueWith(_=>done.Set(),TaskScheduler.Default);
            Marshal.ThrowExceptionForHR(CoWaitForMultipleHandles(0,10000,1,[done.SafeWaitHandle.DangerousGetHandle()],out _));
            Require(confirmation.GetAwaiter().GetResult(),"Actual Shell copied without successful final confirmation");
            for(int index=0;index<files.Length;index++)
            {
                string copy=Path.Combine(destination,files[index].Name);
                Require(File.ReadAllBytes(copy).SequenceEqual(originals[index]) && File.ReadAllBytes(files[index].Path).SequenceEqual(originals[index]),"Actual Shell source/copy bytes changed");
                var attributes=File.GetAttributes(copy);
                Require((attributes&System.IO.FileAttributes.Temporary)==0,"Ordinary copy acquired Temporary");
                Require(((attributes&System.IO.FileAttributes.ReadOnly)!=0)==(index==2),"Copy ReadOnly differs from original");
            }
            Console.WriteLine("INFO Actual Windows folder IDropTarget: three streamed copies, Unicode/empty/read-only, byte-identical originals, source attributes and final S_OK Copy callback; no physical drag or global clipboard.");
        }
        finally
        {
            foreach(nint pointer in new[]{target,item,dataPointer,unknown}) if(pointer!=0) Marshal.Release(pointer);
            if(initialized>=0) OleUninitialize();
            if(Directory.Exists(root))
                foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)) File.SetAttributes(file,System.IO.FileAttributes.Normal);
        }
    }
    private static void Require(bool value,string reason) { if(!value) throw new Exception(reason); }
    private static T Method<T>(nint self,int slot) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self),slot*IntPtr.Size));
    [StructLayout(LayoutKind.Sequential)] private readonly record struct Point(int X,int Y);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Bind(nint self,nint context,in Guid handler,in Guid iid,out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Drag(nint self,nint data,uint keys,Point position,ref uint effect);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path,nint context,in Guid iid,out nint item);
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int CoWaitForMultipleHandles(uint flags,uint milliseconds,uint count,nint[] handles,out uint index);
}
