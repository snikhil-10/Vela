using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;

internal static class Program
{
    private static readonly Guid DataIid=new("0000010E-0000-0000-C000-000000000046");
    private static readonly Guid AsyncIid=new("3D8B0590-F691-11D2-8EA9-006097DF5BD4");
    [STAThread]
    private static int Main(string[] args)
    {
        if(args is ["--receiver",var receiverRoot,var code]) return ReceiveProcess(receiverRoot,int.Parse(code,System.Globalization.CultureInfo.InvariantCulture));
        string root=Path.Combine(Path.GetTempPath(),"Vela-Shelf-Marshalling-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int initialized=CoInitializeEx(0,2);
        try
        {
            Marshal.ThrowExceptionForHR(initialized);
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Require(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA,"Source is not STA");
            var assembly=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Vela.Windows.dll"));
            string candidate=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant();
            Require(candidate=="77513e653d3d502a05402c4d8625980aa957912f93978312a229a02335099e85","Wrong candidate DLL");
            Console.WriteLine("INFO Exact candidate DLL SHA256="+candidate+"; source apartment=STA");
            string profile=Path.Combine(root,"profile"); Directory.CreateDirectory(profile);
            assembly.GetType("Vela.Windows.AppDataPaths",true)!.GetMethod("Configure",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!
                .Invoke(null,[new string[]{"--data-directory",profile}]);
            string[] originals=[Path.Combine(root,"first 東京.txt"),Path.Combine(root,"second.txt")];
            for(int i=0;i<originals.Length;i++) File.WriteAllText(originals[i],"Generated original "+i+" ✓");
            byte[][] originalBytes=originals.Select(File.ReadAllBytes).ToArray();
            var files=originals.Select(path=>(IStorageItem)StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult()).ToArray();
            foreach(int status in new[]{unchecked((int)0x800704C7),unchecked((int)0x80004005),0})
                Transfer(assembly,root,files,originals,originalBytes,status);
            foreach(int status in new[]{unchecked((int)0x800704C7),0})
                ProcessTransfer(assembly,root,files,originals,originalBytes,status);
            for(int i=0;i<originals.Length;i++) Require(File.ReadAllBytes(originals[i]).SequenceEqual(originalBytes[i]),"Original changed");
            Console.WriteLine("PASS Cross-apartment and cross-process native IDataObject payload, Unicode/order, successful copy, cancel/failure completion and unchanged originals; no physical drag or global clipboard.");
            return 0;
        }
        catch(Exception error) { Console.WriteLine("FAIL "+error); return 1; }
        finally { if(initialized>=0) CoUninitialize(); Directory.Delete(root,true); }
    }
    private static void ProcessTransfer(Assembly assembly,string root,IStorageItem[] files,string[] originals,byte[][] originalBytes,int status)
    {
        var type=assembly.GetType("Vela.Windows.Services.ShellDropFeedback",true)!;
        var package=new DataPackage { RequestedOperation=DataPackageOperation.Copy }; package.SetStorageItems(files);
        var feedback=type.GetMethod("Capture")!.Invoke(null,[package,files])??throw new Exception("Capture failed");
        using var owned=(IDisposable)feedback;
        package.As<IDataObjectProvider>().GetDataObject(out var data);
        nint unknown=Marshal.GetIUnknownForObject(data);
        Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(0,true,out var stream));
        bool consumed=false;
        try
        {
            // This capsule contains only the fixture's own generated COM object.
            Marshal.ThrowExceptionForHR(CoMarshalInterface(stream,in DataIid,unknown,0,0,0));
            stream.Stat(out var stat,1); stream.Seek(0,0,0); var capsule=new byte[checked((int)stat.cbSize)]; stream.Read(capsule,capsule.Length,0);
            File.WriteAllBytes(Path.Combine(root,"marshal.bin"),capsule);
            File.WriteAllLines(Path.Combine(root,"paths.txt"),originals);
            string destination=Path.Combine(root,"process-"+unchecked((uint)status).ToString("X8")); Directory.CreateDirectory(destination);
            var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                RedirectStandardOutput=true,RedirectStandardError=true };
            start.ArgumentList.Add("--receiver"); start.ArgumentList.Add(root); start.ArgumentList.Add(status.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var receiver=Process.Start(start)??throw new Exception("Receiver process did not start");
            var output=receiver.StandardOutput.ReadToEndAsync(); var error=receiver.StandardError.ReadToEndAsync();
            int waited=CoWaitForMultipleHandles(0,15000,1,[receiver.Handle],out _);
            if(waited<0) { receiver.Kill(entireProcessTree:true); receiver.WaitForExit(); Marshal.ThrowExceptionForHR(waited); }
            receiver.WaitForExit(); consumed=File.Exists(Path.Combine(root,"consumed"));
            Console.Write(output.GetAwaiter().GetResult());
            Require(receiver.ExitCode==0,"Separate receiver failed: "+error.GetAwaiter().GetResult());
            Require(consumed,"Separate receiver did not unmarshal the fixture");
            File.Delete(Path.Combine(root,"consumed"));
            bool confirmed=((Task<bool>)type.GetMethod("ConfirmCopy")!.Invoke(feedback,[CancellationToken.None])!).GetAwaiter().GetResult();
            Require(confirmed==(status==0),"Cross-process completion status changed");
            if(status==0)
                for(int i=0;i<originals.Length;i++) Require(File.ReadAllBytes(Path.Combine(destination,Path.GetFileName(originals[i]))).SequenceEqual(originalBytes[i]),"Separate-process copy changed bytes");
            else Require(Directory.GetFiles(destination).Length==0,"Canceled separate receiver wrote a copy");
            Console.WriteLine($"PASS Native STA-to-process CF_HDROP and callback HRESULT=0x{status:X8}; confirmed={confirmed}; copies={Directory.GetFiles(destination).Length}.");
        }
        finally
        {
            if(!consumed) { stream.Seek(0,0,0); CoReleaseMarshalData(stream); }
            Marshal.ReleaseComObject(stream); Marshal.Release(unknown);
        }
    }
    private static int ReceiveProcess(string root,int status)
    {
        int initialized=CoInitializeEx(0,2); nint native=0,asynchronous=0; IStream? stream=null;
        try
        {
            Marshal.ThrowExceptionForHR(initialized);
            Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(0,true,out stream));
            byte[] capsule=File.ReadAllBytes(Path.Combine(root,"marshal.bin")); stream.Write(capsule,capsule.Length,0); stream.Seek(0,0,0);
            Marshal.ThrowExceptionForHR(CoUnmarshalInterface(stream,in DataIid,out native)); File.WriteAllText(Path.Combine(root,"consumed"),"yes");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native,in AsyncIid,out asynchronous));
            Marshal.ThrowExceptionForHR(Method<Start>(asynchronous,5)(asynchronous,0));
            var format=new FORMATETC { cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL };
            Marshal.ThrowExceptionForHR(Method<GetData>(native,3)(native,ref format,out var medium));
            try
            {
                Require(medium.tymed==TYMED.TYMED_HGLOBAL,"Separate-process payload is not HGLOBAL");
                string[] expected=File.ReadAllLines(Path.Combine(root,"paths.txt"));
                uint count=DragQueryFileW(medium.unionmember,uint.MaxValue,null,0); Require(count==expected.Length,"Separate-process payload count changed");
                for(uint i=0;i<count;i++)
                {
                    uint length=DragQueryFileW(medium.unionmember,i,null,0); var path=new StringBuilder(checked((int)length+1));
                    Require(DragQueryFileW(medium.unionmember,i,path,(uint)path.Capacity)==length && path.ToString()==expected[i],"Separate-process Unicode/order changed");
                    if(status==0) File.Copy(path.ToString(),Path.Combine(root,"process-00000000",Path.GetFileName(path.ToString())));
                }
            }
            finally { ReleaseStgMedium(ref medium); }
            Marshal.ThrowExceptionForHR(Method<End>(asynchronous,7)(asynchronous,status,0,1));
            Console.WriteLine("INFO Separate receiver read both original paths and returned native completion"); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if(asynchronous!=0) Marshal.Release(asynchronous); if(native!=0) Marshal.Release(native);
            if(stream is not null) Marshal.ReleaseComObject(stream); if(initialized>=0) CoUninitialize();
        }
    }
    private static void Transfer(Assembly assembly,string root,IStorageItem[] files,string[] originals,byte[][] originalBytes,int status)
    {
        var type=assembly.GetType("Vela.Windows.Services.ShellDropFeedback",true)!;
        var package=new DataPackage { RequestedOperation=DataPackageOperation.Copy }; package.SetStorageItems(files);
        var feedback=type.GetMethod("Capture")!.Invoke(null,[package,files])??throw new Exception("Capture failed");
        using var owned=(IDisposable)feedback;
        package.As<IDataObjectProvider>().GetDataObject(out var data);
        nint unknown=Marshal.GetIUnknownForObject(data),stream=0;
        using var done=new EventWaitHandle(false,EventResetMode.ManualReset);
        Exception? failure=null;
        string destination=Path.Combine(root,"destination-"+unchecked((uint)status).ToString("X8")); Directory.CreateDirectory(destination);
        try
        {
            Marshal.ThrowExceptionForHR(CoMarshalInterThreadInterfaceInStream(in DataIid,unknown,out stream));
            nint targetStream=stream; stream=0;
            var target=new Thread(()=>
            {
                int targetInitialized=CoInitializeEx(0,0); nint native=0,asyncNative=0;
                try
                {
                    Marshal.ThrowExceptionForHR(targetInitialized);
                    Require(Thread.CurrentThread.GetApartmentState()==ApartmentState.MTA,"Target is not MTA");
                    Marshal.ThrowExceptionForHR(CoGetInterfaceAndReleaseStream(targetStream,in DataIid,out native)); targetStream=0;
                    Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native,in AsyncIid,out asyncNative));
                    Marshal.ThrowExceptionForHR(Method<GetFlag>(asyncNative,4)(asyncNative,out int asynchronous)); Require(asynchronous==1,"Async BOOL changed");
                    Marshal.ThrowExceptionForHR(Method<Start>(asyncNative,5)(asyncNative,0));
                    var format=new FORMATETC { cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL };
                    Marshal.ThrowExceptionForHR(Method<GetData>(native,3)(native,ref format,out var medium));
                    try
                    {
                        Require(medium.tymed==TYMED.TYMED_HGLOBAL,"Payload was not CF_HDROP HGLOBAL");
                        uint count=DragQueryFileW(medium.unionmember,uint.MaxValue,null,0); Require(count==originals.Length,"Payload count changed");
                        for(uint i=0;i<count;i++)
                        {
                            uint length=DragQueryFileW(medium.unionmember,i,null,0); var path=new StringBuilder(checked((int)length+1));
                            Require(DragQueryFileW(medium.unionmember,i,path,(uint)path.Capacity)==length,"Path truncated");
                            Require(path.ToString()==originals[i],"Payload order or Unicode changed");
                            if(status==0) File.Copy(path.ToString(),Path.Combine(destination,Path.GetFileName(path.ToString())));
                        }
                    }
                    finally { ReleaseStgMedium(ref medium); }
                    Marshal.ThrowExceptionForHR(Method<End>(asyncNative,7)(asyncNative,status,0,1));
                }
                catch(Exception error) { failure=error; }
                finally
                {
                    if(targetStream!=0) Marshal.Release(targetStream);
                    if(asyncNative!=0) Marshal.Release(asyncNative); if(native!=0) Marshal.Release(native);
                    if(targetInitialized>=0) CoUninitialize(); done.Set();
                }
            }) { IsBackground=true };
            target.SetApartmentState(ApartmentState.MTA); target.Start();
            Marshal.ThrowExceptionForHR(CoWaitForMultipleHandles(0,15000,1,[done.SafeWaitHandle.DangerousGetHandle()],out _));
            target.Join(1000);
            if(failure is not null) throw new Exception("MTA receiver failed",failure);
            bool confirmed=((Task<bool>)type.GetMethod("ConfirmCopy")!.Invoke(feedback,[CancellationToken.None])!).GetAwaiter().GetResult();
            Require(confirmed==(status==0),"Cross-apartment completion status changed");
            if(status==0)
                for(int i=0;i<originals.Length;i++) Require(File.ReadAllBytes(Path.Combine(destination,Path.GetFileName(originals[i]))).SequenceEqual(originalBytes[i]),"Copy bytes changed");
            else Require(Directory.GetFiles(destination).Length==0,"Canceled receiver wrote a copy");
            Console.WriteLine($"PASS Native STA-to-MTA CF_HDROP and completion HRESULT=0x{status:X8}; confirmed={confirmed}; copies={Directory.GetFiles(destination).Length}.");
        }
        finally { if(stream!=0) Marshal.Release(stream); Marshal.Release(unknown); }
    }
    private static void Require(bool value,string reason) { if(!value) throw new Exception(reason); }
    private static T Method<T>(nint instance,int slot) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetData(nint self,ref FORMATETC format,out STGMEDIUM medium);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetFlag(nint self,out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Start(nint self,nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int End(nint self,int status,nint reserved,uint effect);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved,uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoMarshalInterThreadInterfaceInStream(in Guid iid,nint unknown,out nint stream);
    [DllImport("ole32.dll")] private static extern int CoGetInterfaceAndReleaseStream(nint stream,in Guid iid,out nint value);
    [DllImport("ole32.dll")] private static extern int CreateStreamOnHGlobal(nint memory,[MarshalAs(UnmanagedType.Bool)]bool delete,out IStream stream);
    [DllImport("ole32.dll")] private static extern int CoMarshalInterface(IStream stream,in Guid iid,nint unknown,uint context,nint reserved,uint flags);
    [DllImport("ole32.dll")] private static extern int CoUnmarshalInterface(IStream stream,in Guid iid,out nint value);
    [DllImport("ole32.dll")] private static extern int CoReleaseMarshalData(IStream stream);
    [DllImport("ole32.dll")] private static extern int CoWaitForMultipleHandles(uint flags,uint milliseconds,uint count,nint[] handles,out uint index);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern uint DragQueryFileW(nint drop,uint index,StringBuilder? file,uint size);
    [ComImport,Guid("3D25F6D6-4B2A-433C-9184-7C33AD35D001"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataObjectProvider
    {
        void GetDataObject([MarshalAs(UnmanagedType.Interface)]out IDataObject data);
        void SetDataObject([MarshalAs(UnmanagedType.Interface)]IDataObject data);
    }
}

