using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root=Path.Combine(Path.GetTempPath(),"Vela-Actual-Shell-Target-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        int initialized=OleInitialize(0); nint item=0,target=0,dataPointer=0,unknown=0;
        try
        {
            Marshal.ThrowExceptionForHR(initialized); WinRT.ComWrappersSupport.InitializeComWrappers();
            var assembly=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Vela.Windows.dll"));
            Console.WriteLine("INFO Exact candidate DLL SHA256="+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant());
            string profile=Path.Combine(root,"profile"); Directory.CreateDirectory(profile);
            assembly.GetType("Vela.Windows.AppDataPaths",true)!.GetMethod("Configure",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!
                .Invoke(null,[new string[]{"--data-directory",profile}]);
            string source=Path.Combine(root,"source"),destination=Path.Combine(root,"empty-destination"); Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
            var files=new List<IStorageItem>();
            foreach(string name in new[]{"one 東京.txt","two.txt"})
            {
                string path=Path.Combine(source,name); File.WriteAllText(path,"Generated Shell transfer: "+name);
                files.Add(StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult());
            }
            bool collision=args.Contains("--collision"),skip=args.Contains("--skip");
            if(collision || skip)
                foreach(var file in files.Take(skip?1:2)) File.WriteAllText(Path.Combine(destination,file.Name),"Generated OLD destination");
            // Model Explorer's read-only WinRT access proxies, then the shelf's
            // actual import/export path before calling the native folder target.
            var incoming=new DataPackage(); incoming.SetStorageItems(files);
            var shelfType=assembly.GetType("Vela.Windows.Services.ShelfService",true)!;
            var shelf=Activator.CreateInstance(shelfType,nonPublic:true)!;
            ((Task<int>)shelfType.GetMethod("Add")!.Invoke(shelf,[incoming.GetView(),CancellationToken.None])!).GetAwaiter().GetResult();
            var entries=((System.Collections.IEnumerable)shelfType.GetProperty("Items")!.GetValue(shelf)!).Cast<object>().ToArray();
            var selected=Array.CreateInstance(assembly.GetType("Vela.Windows.Services.ShelfItem",true)!,entries.Length);
            for(int index=0;index<entries.Length;index++) selected.SetValue(entries[index],index);
            var exported=entries.Select(entry=>(IStorageItem)entry.GetType().GetProperty("File")!.GetValue(entry)!).ToArray();
            var package=(DataPackage)shelfType.GetMethod("Package")!.Invoke(null,[selected])!;
            var feedbackType=assembly.GetType("Vela.Windows.Services.ShellDropFeedback",true)!;
            object prepared;
            if(args.Contains("--flat") || args.Contains("--virtual"))
            {
                var sourcePackage=new DataPackage { RequestedOperation=DataPackageOperation.Copy }; sourcePackage.SetStorageItems(files);
                sourcePackage.As<IDataObjectProvider>().GetDataObject(out var inner);
                var transfer=Activator.CreateInstance(assembly.GetType("Vela.Windows.Services.TrackedShelfDataObject",true)!,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[sourcePackage,new FlatData(inner,args.Contains("--virtual"))],null)!;
                prepared=Activator.CreateInstance(feedbackType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[transfer],null)!;
                package.As<IDataObjectProvider>().SetDataObject((IDataObject)transfer);
                Console.WriteLine("INFO Diagnostic: "+(args.Contains("--virtual")?"virtual FILECONTENTS/FILEDESCRIPTOR":"CF_HDROP")+" and Preferred DropEffect offered");
            }
            else prepared=feedbackType.GetMethod("Capture")!.Invoke(null,[package,exported])??throw new Exception("No tracking adapter");
            using var feedback=(IDisposable)prepared;
            package.As<IDataObjectProvider>().GetDataObject(out var data);
            unknown=Marshal.GetIUnknownForObject(data); Guid dataIid=new("0000010E-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown,in dataIid,out dataPointer));
            Guid itemIid=new("43826D1E-E718-42EE-BC55-A1E261C37BFE"),handler=new("3981E225-F559-11D3-8E3A-00C04F6837D5"),dropIid=new("00000122-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(destination,0,in itemIid,out item));
            Marshal.ThrowExceptionForHR(Method<Bind>(item,3)(item,0,in handler,in dropIid,out target));
            uint effect=1;
            Marshal.ThrowExceptionForHR(Method<Drag>(target,3)(target,dataPointer,9,new(0,0),ref effect));
            Console.WriteLine("INFO Actual Windows folder IDropTarget::DragEnter effect="+effect);
            if(effect!=1) throw new Exception("Actual Windows folder rejected Copy");
            effect=1;
            Marshal.ThrowExceptionForHR(Method<Drag>(target,6)(target,dataPointer,8,new(0,0),ref effect));
            Console.WriteLine("INFO Actual Windows folder IDropTarget::Drop effect="+effect);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(collision || skip?90:8));
            var completion=(Task<bool>)feedbackType.GetMethod("ConfirmCopy")!.Invoke(feedback,[timeout.Token])!;
            using var done=new EventWaitHandle(false,EventResetMode.ManualReset);
            _=completion.ContinueWith(_=>done.Set(),TaskScheduler.Default);
            Marshal.ThrowExceptionForHR(CoWaitForMultipleHandles(0,collision || skip?95000u:10000u,1,[done.SafeWaitHandle.DangerousGetHandle()],out _));
            bool confirmed=false;
            try { confirmed=completion.GetAwaiter().GetResult(); } catch(OperationCanceledException) { }
            Console.WriteLine("INFO Actual Shell async completion confirmed="+confirmed);
            if(collision || skip)
            {
                if(!completion.IsCompletedSuccessfully) throw new Exception("Collision fixture timed out without a completed native callback");
                if(confirmed) throw new Exception("Canceled or partly skipped Shell copy was incorrectly confirmed");
                foreach(var file in files.Take(skip?1:2))
                    if(File.ReadAllText(Path.Combine(destination,file.Name))!="Generated OLD destination") throw new Exception("An old destination changed after cancel/skip");
                if(skip && !File.ReadAllBytes(files[1].Path).SequenceEqual(File.ReadAllBytes(Path.Combine(destination,files[1].Name)))) throw new Exception("The unskipped copy is missing or changed");
                Console.WriteLine(skip?"PASS Actual Shell partial skip rejects complete removal and preserves old/new bytes.":"PASS Actual Shell collision cancellation rejects removal and preserves old destination bytes."); return 0;
            }
            foreach(var file in files)
            {
                string copy=Path.Combine(destination,file.Name);
                if(!File.Exists(copy) || !File.ReadAllBytes(file.Path).SequenceEqual(File.ReadAllBytes(copy))) throw new Exception("Actual Shell copy missing or differs: "+file.Name);
                if((File.GetAttributes(copy)&(System.IO.FileAttributes.ReadOnly|System.IO.FileAttributes.Temporary))!=0) throw new Exception("Writable source became a read-only/temporary copy");
            }
            Console.WriteLine("PASS Actual Windows folder handler copied both generated originals byte-identically.");
            if(!confirmed) throw new Exception("Actual Shell copied successfully but final callback was not confirmed");
            Console.WriteLine("PASS Actual Windows folder drop handler and final successful async callback; no mouse injection, global clipboard or existing files."); return 0;
        }
        catch(Exception error) { Console.WriteLine("FAIL "+error); return 1; }
        finally
        {
            foreach(nint pointer in new[]{target,item,dataPointer,unknown}) if(pointer!=0) Marshal.Release(pointer);
            if(initialized>=0) OleUninitialize();
            string trace=Path.Combine(root,"profile","shelf-transfer.log"); if(File.Exists(trace)) File.Copy(trace,Path.Combine(AppContext.BaseDirectory,"shell-target-trace.log"),true);
            foreach(var path in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
            { Console.WriteLine("INFO Cleanup generated file attributes="+File.GetAttributes(path)); File.SetAttributes(path,System.IO.FileAttributes.Normal); }
            Directory.Delete(root,true);
        }
    }
    private static T Method<T>(nint self,int slot) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self),slot*IntPtr.Size));
    [StructLayout(LayoutKind.Sequential)] private readonly record struct Point(int X,int Y);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Bind(nint self,nint context,in Guid handler,in Guid iid,out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Drag(nint self,nint data,uint keys,Point position,ref uint effect);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path,nint context,in Guid iid,out nint item);
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int CoWaitForMultipleHandles(uint flags,uint milliseconds,uint count,nint[] handles,out uint index);
    [ComImport,Guid("3D25F6D6-4B2A-433C-9184-7C33AD35D001"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataObjectProvider
    {
        void GetDataObject([MarshalAs(UnmanagedType.Interface)]out IDataObject data);
        void SetDataObject([MarshalAs(UnmanagedType.Interface)]IDataObject data);
    }
}

[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class FlatData(IDataObject inner,bool virtualFiles) : IDataObject
{
    private static readonly short Preferred=unchecked((short)RegisterClipboardFormatW("Preferred DropEffect"));
    private static readonly short Descriptor=unchecked((short)RegisterClipboardFormatW("FileGroupDescriptorW")),Contents=unchecked((short)RegisterClipboardFormatW("FileContents"));
    private bool Allowed(FORMATETC format)=>format.cfFormat==Preferred || (virtualFiles?format.cfFormat==Descriptor || format.cfFormat==Contents:format.cfFormat==15);
    public void GetData(ref FORMATETC format,out STGMEDIUM medium)
    { if(!Allowed(format)) throw new COMException("Unsupported format",unchecked((int)0x8004006A)); inner.GetData(ref format,out medium); }
    public void GetDataHere(ref FORMATETC format,ref STGMEDIUM medium)=>inner.GetDataHere(ref format,ref medium);
    public int QueryGetData(ref FORMATETC format)=>Allowed(format)?inner.QueryGetData(ref format):unchecked((int)0x8004006A);
    public int GetCanonicalFormatEtc(ref FORMATETC format,out FORMATETC canonical)=>inner.GetCanonicalFormatEtc(ref format,out canonical);
    public void SetData(ref FORMATETC format,ref STGMEDIUM medium,bool release)=>inner.SetData(ref format,ref medium,release);
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        var formats=new List<FORMATETC> {new(){cfFormat=virtualFiles?Descriptor:(short)15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL},new(){cfFormat=Preferred,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL}};
        if(virtualFiles) formats.Add(new(){cfFormat=Contents,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_ISTREAM});
        Marshal.ThrowExceptionForHR(SHCreateStdEnumFmtEtc((uint)formats.Count,formats.ToArray(),out var result)); return result;
    }
    public int DAdvise(ref FORMATETC format,ADVF flags,IAdviseSink sink,out int connection)=>inner.DAdvise(ref format,flags,sink,out connection);
    public void DUnadvise(int connection)=>inner.DUnadvise(connection);
    public int EnumDAdvise(out IEnumSTATDATA? result)=>inner.EnumDAdvise(out result);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("shell32.dll")] private static extern int SHCreateStdEnumFmtEtc(uint count,[In]FORMATETC[] formats,out IEnumFORMATETC result);
}
