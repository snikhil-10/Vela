using System.ComponentModel;
using System.Runtime.InteropServices;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed record TextPublication(uint Sequence,Exception? Error);

internal static class TextClipboardPublisher
{
    internal static async Task<TextPublication?> Publish(nint owner,uint expectedSequence,string text,CancellationToken token)
    {
        if(owner==0) throw new ArgumentException("A clipboard owner window is required.",nameof(owner));
        if(text.Contains('\0')) throw new InvalidOperationException("Text containing a null character cannot be pasted safely.");
        // Prepare every allocation before touching the original clipboard.
        using var content=new GlobalBuffer(checked((text.Length+1)*2));
        content.Fill(pointer=> { Marshal.Copy(text.ToCharArray(),0,pointer,text.Length); Marshal.WriteInt16(pointer,text.Length*2,0); });
        using var history=new GlobalBuffer(4); history.Fill(pointer=>Marshal.WriteInt32(pointer,0));
        using var roaming=new GlobalBuffer(4); roaming.Fill(pointer=>Marshal.WriteInt32(pointer,0));
        uint historyFormat=RegisterClipboardFormatW("CanIncludeInClipboardHistory");
        uint roamingFormat=RegisterClipboardFormatW("CanUploadToCloudClipboard");
        if(historyFormat==0 || roamingFormat==0) throw new Win32Exception(Marshal.GetLastWin32Error());
        uint ownedSequence=0;
        Exception? failure=null;
        bool published=await ClipboardPreparation.Write(expectedSequence,Native.GetClipboardSequenceNumber,()=>
        {
            if(!OpenClipboard(owner)) return false;
            bool changed=false;
            try
            {
                // Check inside the OS lock too: a copy can occur between the
                // retry policy's check and successfully acquiring the clipboard.
                if(Native.GetClipboardSequenceNumber()!=expectedSequence) return false;
                if(!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error());
                changed=true;
                history.Transfer(historyFormat); roaming.Transfer(roamingFormat);
                content.Transfer(13); // CF_UNICODETEXT, immediately rendered.
            }
            catch(Exception error) when(changed) { failure=error; }
            finally
            {
                CloseClipboard();
                // Closing synthesizes Windows' accompanying text formats and
                // advances the sequence. Read it after close, then check owner:
                // a different copy before this read must never be adopted as
                // ours. A copy after this check changes the captured sequence
                // and is still protected by the restoration policy.
                if(changed)
                {
                    ownedSequence=Native.GetClipboardSequenceNumber();
                    if(GetClipboardOwner()!=owner) ownedSequence=0;
                }
            }
            return true;
        },token);
        return published && ownedSequence!=0?new(ownedSequence,failure):null;
    }
    private sealed class GlobalBuffer:IDisposable
    {
        private nint handle;
        internal GlobalBuffer(int bytes)
        {
            handle=GlobalAlloc(0x42,(nuint)bytes); // GMEM_MOVEABLE | GMEM_ZEROINIT
            if(handle==0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        internal void Fill(Action<nint> fill)
        {
            nint pointer=GlobalLock(handle);
            if(pointer==0) throw new Win32Exception(Marshal.GetLastWin32Error());
            try { fill(pointer); } finally { GlobalUnlock(handle); }
        }
        internal void Transfer(uint format)
        {
            if(SetClipboardData(format,handle)==0) throw new Win32Exception(Marshal.GetLastWin32Error());
            handle=0; // Windows now owns and frees this allocation.
        }
        public void Dispose() { if(handle!=0) { GlobalFree(handle); handle=0; } }
    }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool OpenClipboard(nint owner);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern nint GetClipboardOwner();
    [DllImport("user32.dll",SetLastError=true)] private static extern nint SetClipboardData(uint format,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint memory);
}
