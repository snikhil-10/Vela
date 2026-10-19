using System.Runtime.InteropServices;

namespace Vela.Windows.Services;

internal static class MicrophoneService
{
    internal sealed record Device(string Name,bool Muted);
    private static readonly Guid EndpointVolume=new("5CDF2C82-841E-4546-9722-0CF74078229A");
    public static IReadOnlyList<Device> List()
    {
        var result=new List<Device>();
        Visit((device,volume)=> { volume.GetMute(out bool muted); result.Add(new(Name(device),muted)); });
        return result;
    }
    public static void SetAll(bool muted)=>Visit((device,volume)=> { var context=Guid.Empty; volume.SetMute(muted,ref context); });
    private static void Visit(Action<IMMDevice,IAudioEndpointVolume> action)
    {
        var enumerator=(IMMDeviceEnumerator)new MMDeviceEnumerator(); IMMDeviceCollection? collection=null;
        try
        {
            enumerator.EnumAudioEndpoints(1,1,out collection); collection.GetCount(out uint count);
            for(uint i=0;i<count;i++)
            {
                collection.Item(i,out var device); object? endpoint=null;
                try { var iid=EndpointVolume; device.Activate(ref iid,23,0,out endpoint); action(device,(IAudioEndpointVolume)endpoint); }
                finally { if(endpoint is not null) Marshal.ReleaseComObject(endpoint); Marshal.ReleaseComObject(device); }
            }
        }
        finally { if(collection is not null) Marshal.ReleaseComObject(collection); Marshal.ReleaseComObject(enumerator); }
    }
    private static string Name(IMMDevice device)
    {
        device.OpenPropertyStore(0,out var store);
        try
        {
            var key=new PropertyKey { Format=new("A45C254E-DF1C-4EFD-8020-67D146A850E0"),Id=14 };
            store.GetValue(ref key,out var value);
            try { return value.Type==31?Marshal.PtrToStringUni(value.Pointer)??"Microphone":"Microphone"; }
            finally { PropVariantClear(ref value); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit,Size=24)] private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; }
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumerator { }
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int flow,uint state,out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IMMDevice device);
        void RegisterEndpointNotificationCallback(nint callback);
        void UnregisterEndpointNotificationCallback(nint callback);
    }
    [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection { void GetCount(out uint count); void Item(uint index,out IMMDevice device); }
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid,uint context,nint activation,[MarshalAs(UnmanagedType.IUnknown)]out object instance);
        void OpenPropertyStore(uint access,out IPropertyStore store);
        void GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);
        void GetState(out uint state);
    }
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count); void GetAt(uint index,out PropertyKey key);
        void GetValue(ref PropertyKey key,out PropVariant value);
        void SetValue(ref PropertyKey key,ref PropVariant value); void Commit();
    }
    [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(nint callback); void UnregisterControlChangeNotify(nint callback);
        void GetChannelCount(out uint count); void SetMasterVolumeLevel(float level,ref Guid context);
        void SetMasterVolumeLevelScalar(float level,ref Guid context); void GetMasterVolumeLevel(out float level);
        void GetMasterVolumeLevelScalar(out float level); void SetChannelVolumeLevel(uint channel,float level,ref Guid context);
        void SetChannelVolumeLevelScalar(uint channel,float level,ref Guid context); void GetChannelVolumeLevel(uint channel,out float level);
        void GetChannelVolumeLevelScalar(uint channel,out float level); void SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,ref Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)]out bool muted);
    }
}
