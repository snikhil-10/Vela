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
        string root=Path.Combine(Path.GetTempPath(),"Vela-Drag-Discovery-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string path=Path.Combine(root,"original.txt"); File.WriteAllText(path,"Generated source");
            var file=StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult();
            var package=new DataPackage { RequestedOperation=DataPackageOperation.Copy }; package.SetStorageItems([file]);
            package.As<IDataObjectProvider>().GetDataObject(out var original); Inspect("WinRT original",original);
            var assembly=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Vela.Windows.dll"));
            var type=assembly.GetType("Vela.Windows.Services.ShellDropFeedback",true)!;
            using var feedback=(IDisposable)(type.GetMethod("Capture")!.Invoke(null,[package,new IStorageItem[]{file}])??throw new Exception("Capture failed"));
            package.As<IDataObjectProvider>().GetDataObject(out var wrapped); Inspect("Tracked native candidate",wrapped);
            return 0;
        }
        catch(Exception error) { Console.WriteLine("FAIL "+error); return 1; }
        finally { Directory.Delete(root,true); }
    }
    private static void Inspect(string label,IDataObject data)
    {
        Console.WriteLine("INFO "+label);
        var drop=new FORMATETC { cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL };
        Console.WriteLine($"INFO QueryGetData(CF_HDROP)=0x{data.QueryGetData(ref drop):X8}");
        var formats=data.EnumFormatEtc(DATADIR.DATADIR_GET);
        try
        {
            int[] fetched=[0]; FORMATETC[] next=new FORMATETC[1]; int count=0;
            while(formats.Next(1,next,fetched)==0 && fetched[0]==1 && count++<64)
            {
                var format=next[0];
                Console.WriteLine($"INFO Format={unchecked((ushort)format.cfFormat)} aspect={format.dwAspect} index={format.lindex} medium={format.tymed}; query=0x{data.QueryGetData(ref format):X8}");
                if(format.ptd!=0) Marshal.FreeCoTaskMem(format.ptd);
            }
        }
        finally { if(Marshal.IsComObject(formats)) Marshal.ReleaseComObject(formats); }
    }
    [ComImport,Guid("3D25F6D6-4B2A-433C-9184-7C33AD35D001"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataObjectProvider
    {
        void GetDataObject([MarshalAs(UnmanagedType.Interface)]out IDataObject data);
        void SetDataObject([MarshalAs(UnmanagedType.Interface)]IDataObject data);
    }
}
