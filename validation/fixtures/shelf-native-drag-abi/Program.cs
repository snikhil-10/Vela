using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        string root=Path.Combine(Path.GetTempPath(),"Vela-Shelf-Abi-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(root).GetAwaiter().GetResult(); return 0; }
        catch(Exception error) { Console.WriteLine("FAIL "+error); return 1; }
        finally { Directory.Delete(root,true); }
    }
    private static async Task Run(string root)
    {
        string path=Path.Combine(root,"original.txt"); await File.WriteAllTextAsync(path,"Generated ABI original");
        var file=await StorageFile.GetFileFromPathAsync(path);
        var assembly=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Vela.Windows.dll"));
        Console.WriteLine("INFO Candidate DLL SHA256="+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant());
        var type=assembly.GetType("Vela.Windows.Services.ShellDropFeedback",true)!;
        foreach(int status in new[]{unchecked((int)0x800704C7),unchecked((int)0x80004005),1,0})
        {
            var package=new DataPackage { RequestedOperation=DataPackageOperation.Copy }; package.SetStorageItems([file]);
            var feedback=type.GetMethod("Capture")!.Invoke(null,[package,new IStorageItem[]{file}])??throw new Exception("Missing native adapter");
            using var owned=(IDisposable)feedback;
            package.As<IDataObjectProvider>().GetDataObject(out var data);
            nint unknown=Marshal.GetIUnknownForObject(data),native=0;
            try
            {
                Guid iid=new("3D8B0590-F691-11D2-8EA9-006097DF5BD4");
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown,in iid,out native));
                var get=Method<GetFlag>(native,4); var start=Method<Start>(native,5);
                var active=Method<GetFlag>(native,6); var end=Method<End>(native,7);
                Marshal.ThrowExceptionForHR(get(native,out int enabled)); Require(enabled==1,"Native BOOL async mode was not true");
                Marshal.ThrowExceptionForHR(active(native,out int running)); Require(running==0,"Native adapter started active");
                Marshal.ThrowExceptionForHR(start(native,0));
                Marshal.ThrowExceptionForHR(active(native,out running)); Require(running==1,"Native StartOperation did not set active");
                Marshal.ThrowExceptionForHR(end(native,status,0,1));
                Marshal.ThrowExceptionForHR(active(native,out running)); Require(running==0,"Native EndOperation did not clear active");
                bool confirmed=await (Task<bool>)type.GetMethod("ConfirmCopy")!.Invoke(feedback,[CancellationToken.None])!;
                Require(confirmed==(status==0),"Native EndOperation HRESULT was misinterpreted");
                Console.WriteLine($"PASS Native IUnknown QI/vtable BOOL/start/end HRESULT=0x{status:X8}, Copy confirmed={confirmed}.");
            }
            finally { if(native!=0) Marshal.Release(native); Marshal.Release(unknown); }
        }
        Require(await File.ReadAllTextAsync(path)=="Generated ABI original","Native callback changed original");
        ProbeDragSource(assembly);
        Console.WriteLine("PASS Exact-candidate native COM transfer/drop-source ABI; original unchanged; no clipboard or resident app used.");
    }
    private static void ProbeDragSource(Assembly assembly)
    {
        var type=assembly.GetType("Vela.Windows.Services.ShelfNativeDragSource",true)!;
        var source=Activator.CreateInstance(type,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[CancellationToken.None],null)!;
        nint unknown=Marshal.GetIUnknownForObject(source),native=0;
        try
        {
            Guid iid=new("00000121-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown,in iid,out native));
            var query=Method<QueryDrag>(native,3); var feedback=Method<Feedback>(native,4);
            Require(query(native,0,1)==0,"Native drag did not continue while the left button is held");
            Require(query(native,0,0)==0x00040100,"Native drag release did not return DROP");
            Require(query(native,1,1)==0x00040101,"Native drag Escape did not return CANCEL");
            Require(feedback(native,1)==0x00040102,"Native drag did not use standard OLE cursors");
            Console.WriteLine("PASS Native IDropSource QI/vtable PreserveSig: continue, release, Escape and default cursors.");
        }
        finally { if(native!=0) Marshal.Release(native); Marshal.Release(unknown); }
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryDrag(nint self,int escape,uint buttons);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Feedback(nint self,uint effect);
    private static T Method<T>(nint instance,int slot) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size));
    private static void Require(bool value,string reason) { if(!value) throw new Exception(reason); }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetFlag(nint self,out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Start(nint self,nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int End(nint self,int status,nint reserved,uint effect);
    [ComImport,Guid("3D25F6D6-4B2A-433C-9184-7C33AD35D001"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataObjectProvider
    {
        void GetDataObject([MarshalAs(UnmanagedType.Interface)]out IDataObject data);
        void SetDataObject([MarshalAs(UnmanagedType.Interface)]IDataObject data);
    }
}
