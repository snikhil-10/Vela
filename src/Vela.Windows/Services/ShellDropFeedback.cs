using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;
using FileAttributes=System.IO.FileAttributes;

namespace Vela.Windows.Services;

// A reported drop effect can precede Explorer's collision dialog. Only the
// explicit async EndOperation callback can confirm this transfer's completion.
internal sealed class ShellDropFeedback(TrackedShelfDataObject transfer) : IDisposable
{
    internal const string LogicalEffect="Logical Performed DropEffect";
    private bool disposed;
    public static ShellDropFeedback? Capture(DataPackage package,IReadOnlyList<IStorageItem> files)
    {
        try
        {
            // Use an independent package: wrapping the original package's own
            // adapter and putting it back into that package would recurse.
            var source=new DataPackage { RequestedOperation=DataPackageOperation.Copy };
            source.SetStorageItems(files,readOnly:false);
            source.As<IDataObjectProvider>().GetDataObject(out var inner);
            if(files.All(item=>item is IStorageFile && !string.IsNullOrEmpty(item.Path)))
                inner=new ShelfStreamDataObject(inner,files.Select(item=>(uint)(File.GetAttributes(item.Path)
                    & (FileAttributes.ReadOnly|FileAttributes.Hidden|FileAttributes.System|FileAttributes.Archive))).ToArray());
            var transfer=new TrackedShelfDataObject(source,inner);
            package.As<IDataObjectProvider>().SetDataObject(transfer);
            return new(transfer);
        }
        catch(Exception error) when(error is COMException or InvalidCastException or NotSupportedException) { return null; }
    }
    internal DataPackageOperation? Read()=>disposed?null:transfer.Result;
    internal void Trace(string message)=>transfer.Trace(message);
    public async Task<bool> ConfirmCopy(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(disposed) return false;
        try { return await transfer.Completion.WaitAsync(TimeSpan.FromSeconds(30),cancellation)==DataPackageOperation.Copy; }
        catch(TimeoutException) { return false; }
    }
    public void Dispose()
    {
        disposed=true;
        // A target may still be using the adapter after timeout/window close.
        // Its CCW retains the inner object for that target's lifetime; normal
        // COM/GC ownership releases it after the last native reference goes away.
    }

    [ComImport,Guid("3D25F6D6-4B2A-433C-9184-7C33AD35D001"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDataObjectProvider
    {
        void GetDataObject([MarshalAs(UnmanagedType.Interface)]out IDataObject data);
        void SetDataObject([MarshalAs(UnmanagedType.Interface)]IDataObject data);
    }
}

// Filesystem-path extraction can finish without a final async callback. Offering
// the file descriptors/streams makes the receiver complete data extraction.
// WinUI still owns selection, pointer capture and the drag image.
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class ShelfStreamDataObject(IDataObject inner,uint[] attributes) : IDataObject
{
    private static readonly short Preferred=Format("Preferred DropEffect"),Descriptor=Format("FileGroupDescriptorW"),Contents=Format("FileContents");
    private static short Format(string name)=>unchecked((short)RegisterClipboardFormatW(name));
    private static bool Allowed(FORMATETC format)=>format.cfFormat==Preferred || format.cfFormat==Descriptor || format.cfFormat==Contents;
    public void GetData(ref FORMATETC format,out STGMEDIUM medium)
    {
        if(!Allowed(format)) throw new COMException("Unsupported transfer format.",unchecked((int)0x8004006A));
        inner.GetData(ref format,out medium);
        if(format.cfFormat==Descriptor) DescriptorAttributes(ref medium);
    }
    private void DescriptorAttributes(ref STGMEDIUM medium)
    {
        // WinRT marks virtual copies ReadOnly/Temporary. These are ordinary
        // source files, so retain their real user-facing attribute bits instead.
        var original=medium; medium=default; nint memory=0;
        try
        {
            int length=checked(4+attributes.Length*592);
            if(original.tymed!=TYMED.TYMED_HGLOBAL || GlobalSize(original.unionmember)<(nuint)length)
                throw new COMException("Invalid file descriptors.",unchecked((int)0x80040064));
            byte[] bytes=new byte[length]; nint pointer=GlobalLock(original.unionmember);
            if(pointer==0) throw new COMException("Cannot read file descriptors.");
            try { Marshal.Copy(pointer,bytes,0,length); } finally { GlobalUnlock(original.unionmember); }
            if(BitConverter.ToUInt32(bytes,0)!=attributes.Length) throw new COMException("Unexpected file descriptor count.");
            for(int index=0;index<attributes.Length;index++)
            {
                int start=4+index*592;
                BitConverter.TryWriteBytes(bytes.AsSpan(start,4),BitConverter.ToUInt32(bytes,start)|4u);
                BitConverter.TryWriteBytes(bytes.AsSpan(start+36,4),attributes[index]==0?128u:attributes[index]);
            }
            memory=GlobalAlloc(0x42,(nuint)length); if(memory==0) throw new OutOfMemoryException();
            pointer=GlobalLock(memory); if(pointer==0) throw new COMException("Cannot write file descriptors.");
            try { Marshal.Copy(bytes,0,pointer,length); } finally { GlobalUnlock(memory); }
            medium=new STGMEDIUM { tymed=TYMED.TYMED_HGLOBAL,unionmember=memory }; memory=0;
        }
        finally { ReleaseStgMedium(ref original); if(memory!=0) GlobalFree(memory); }
    }
    public void GetDataHere(ref FORMATETC format,ref STGMEDIUM medium)
    { if(!Allowed(format)) throw new COMException("Unsupported transfer format.",unchecked((int)0x8004006A)); inner.GetDataHere(ref format,ref medium); }
    public int QueryGetData(ref FORMATETC format)=>Allowed(format)?inner.QueryGetData(ref format):unchecked((int)0x8004006A);
    public int GetCanonicalFormatEtc(ref FORMATETC format,out FORMATETC canonical)=>inner.GetCanonicalFormatEtc(ref format,out canonical);
    public void SetData(ref FORMATETC format,ref STGMEDIUM medium,bool release)=>inner.SetData(ref format,ref medium,release);
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        if(direction!=DATADIR.DATADIR_GET) return inner.EnumFormatEtc(direction);
        FORMATETC[] formats=[new(){cfFormat=Descriptor,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL},
            new(){cfFormat=Contents,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_ISTREAM},
            new(){cfFormat=Preferred,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL}];
        Marshal.ThrowExceptionForHR(SHCreateStdEnumFmtEtc((uint)formats.Length,formats,out var result)); return result;
    }
    public int DAdvise(ref FORMATETC format,ADVF flags,IAdviseSink sink,out int connection)=>inner.DAdvise(ref format,flags,sink,out connection);
    public void DUnadvise(int connection)=>inner.DUnadvise(connection);
    public int EnumDAdvise(out IEnumSTATDATA? result)=>inner.EnumDAdvise(out result);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("shell32.dll")] private static extern int SHCreateStdEnumFmtEtc(uint count,[In]FORMATETC[] formats,out IEnumFORMATETC result);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint memory);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}

[ComVisible(true),Guid("3D8B0590-F691-11D2-8EA9-006097DF5BD4"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShelfTransferAsync
{
    void SetAsyncMode([MarshalAs(UnmanagedType.Bool)]bool asynchronous);
    void GetAsyncMode([MarshalAs(UnmanagedType.Bool)]out bool asynchronous);
    void StartOperation(nint reserved);
    void InOperation([MarshalAs(UnmanagedType.Bool)]out bool active);
    void EndOperation(int result,nint reserved,uint effect);
}

[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class TrackedShelfDataObject(DataPackage source,IDataObject inner) : IDataObject,IShelfTransferAsync
{
    private readonly object gate=new();
    private readonly TaskCompletionSource<DataPackageOperation> completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool asynchronous=true,active,ended;
    private int starts;
    private DataPackageOperation? result;
    private readonly HashSet<string> formatsTraced=[];
    internal Task<DataPackageOperation> Completion=>completion.Task;
    internal DataPackageOperation? Result { get { lock(gate) return result; } }
    public void SetAsyncMode(bool value) { lock(gate) asynchronous=value; Trace("SetAsyncMode "+value); }
    public void GetAsyncMode(out bool value) { lock(gate) value=asynchronous; }
    public void StartOperation(nint reserved)
    { lock(gate) { starts++; active=true; } Trace("StartOperation"); }
    public void InOperation(out bool value) { lock(gate) value=active; }
    public void EndOperation(int status,nint reserved,uint effect)
    {
        DataPackageOperation outcome;
        lock(gate)
        {
            if(ended) return;
            outcome=asynchronous && starts==1 && status==0 && effect==(uint)DataPackageOperation.Copy
                ? DataPackageOperation.Copy : DataPackageOperation.None;
            ended=true; active=false; result=outcome;
        }
        Trace($"EndOperation HRESULT=0x{status:X8} effect={effect} confirmed={outcome==DataPackageOperation.Copy}");
        completion.TrySetResult(outcome);
    }
    public void GetData(ref FORMATETC format,out STGMEDIUM medium)
    {
        TraceFormat($"GetData format={unchecked((ushort)format.cfFormat)} medium={format.tymed}");
        inner.GetData(ref format,out medium); GC.KeepAlive(source);
    }
    public void GetDataHere(ref FORMATETC format,ref STGMEDIUM medium)=>inner.GetDataHere(ref format,ref medium);
    public int QueryGetData(ref FORMATETC format)
    {
        int status=inner.QueryGetData(ref format);
        TraceFormat($"QueryGetData format={unchecked((ushort)format.cfFormat)} medium={format.tymed} HRESULT=0x{status:X8}"); return status;
    }
    public int GetCanonicalFormatEtc(ref FORMATETC format,out FORMATETC canonical)=>inner.GetCanonicalFormatEtc(ref format,out canonical);
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    { TraceFormat("EnumFormatEtc "+direction); return inner.EnumFormatEtc(direction); }
    public int DAdvise(ref FORMATETC format,ADVF flags,IAdviseSink sink,out int connection)=>inner.DAdvise(ref format,flags,sink,out connection);
    public void DUnadvise(int connection)=>inner.DUnadvise(connection);
    public int EnumDAdvise(out IEnumSTATDATA? enumerator)=>inner.EnumDAdvise(out enumerator);
    public void SetData(ref FORMATETC format,ref STGMEDIUM medium,bool release)
    {
        TraceFormat($"SetData format={unchecked((ushort)format.cfFormat)} medium={medium.tymed}");
        // Effects are diagnostic only: they do not prove that every file copied.
        uint id=unchecked((ushort)format.cfFormat);
        foreach(string name in new[]{ShellDropFeedback.LogicalEffect,"Performed DropEffect","Paste Succeeded"})
            if(id==RegisterClipboardFormatW(name) && medium.tymed==TYMED.TYMED_HGLOBAL && GlobalSize(medium.unionmember)>=4)
            {
                nint pointer=GlobalLock(medium.unionmember);
                if(pointer!=0) { try { Trace(name+"="+Marshal.ReadInt32(pointer)); } finally { GlobalUnlock(medium.unionmember); } }
                break;
            }
        inner.SetData(ref format,ref medium,release);
    }
    internal void Trace(string message)
    {
        if(!AppDataPaths.IsValidationProfile) return;
        try { lock(gate) File.AppendAllText(Path.Combine(AppDataPaths.DirectoryPath,"shelf-transfer.log"),DateTimeOffset.Now.ToString("o")+" "+message+Environment.NewLine); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
    }
    private void TraceFormat(string message)
    {
        if(!AppDataPaths.IsValidationProfile) return;
        lock(gate) { if(formatsTraced.Count>=128 || !formatsTraced.Add(message)) return; }
        Trace(message);
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
}


