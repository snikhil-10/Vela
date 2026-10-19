using System.ComponentModel;
using System.Runtime.InteropServices;
using Vela.Core;
using Vela.Windows.Interop;
using Vela.Windows.Services;
using Windows.ApplicationModel.DataTransfer;

// Explicit disposable-clipboard fixture, separate from the ordinary backend suite.
internal static class TextClipboardChecks
{
    internal static int Run()
    {
        string phase="seed disposable clipboard";
        nint owner=0;
        uint owned=0;
        ClipboardSnapshot? saved=null;
        try
        {
            var seed=new DataPackage(); seed.SetText("Vela publication original");
            seed.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<b>Vela publication original</b>"));
            if(!Clipboard.SetContentWithOptions(seed,new ClipboardContentOptions { IsAllowedInHistory=false,IsRoamable=false }))
                throw new InvalidOperationException("Fixture clipboard is busy.");
            Clipboard.Flush();
            uint original=Native.GetClipboardSequenceNumber();
            phase="snapshot disposable clipboard";
            saved=ClipboardPreparation.Read(original,Native.GetClipboardSequenceNumber,
                ()=>ClipboardSnapshot.Capture(Clipboard.GetContent(),default),default).GetAwaiter().GetResult();
            owner=CreateWindowExW(0,"STATIC","Vela clipboard fixture",0,0,0,0,0,-3,0,Native.GetModuleHandle(null),0);
            if(owner==0) throw new Win32Exception(Marshal.GetLastWin32Error());
            const string expected="Mixed CASE ✓\r\nこんにちは";
            phase="publish immediate text";
            var publication=TextClipboardPublisher.Publish(owner,original,expected,default).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Fixture clipboard was superseded.");
            owned=publication.Sequence;
            if(publication.Error is not null) throw publication.Error;
            uint afterPublish=Native.GetClipboardSequenceNumber();
            Console.WriteLine($"Publication probe before read: capturedSequence={owned}; afterPublish={afterPublish}; ownerMatches={GetClipboardOwner()==owner}; unicodeAvailable={IsClipboardFormatAvailable(13)}.");
            phase="read published Unicode";
            string actual=Clipboard.GetContent().GetTextAsync().AsTask().GetAwaiter().GetResult();
            uint afterRead=Native.GetClipboardSequenceNumber();
            Console.WriteLine($"Publication probe: textMatches={actual==expected}; textLength={actual.Length}; capturedSequence={owned}; afterPublish={afterPublish}; afterRead={afterRead}; ownerMatches={GetClipboardOwner()==owner}.");
            Require(actual==expected,"Published Unicode text differs from the known fixture.");
            Require(afterPublish==owned && afterRead==owned,"Immediate publication ownership changed at close or during read.");
            uint history=RegisterClipboardFormatW("CanIncludeInClipboardHistory"),roaming=RegisterClipboardFormatW("CanUploadToCloudClipboard");
            phase="inspect privacy formats";
            bool open=false;
            for(int attempt=0;attempt<6 && !open;attempt++) { open=OpenClipboard(owner); if(!open) Thread.Sleep(40); }
            if(!open) throw new InvalidOperationException("Cannot inspect fixture privacy formats.");
            try
            {
                foreach(uint format in new[]{history,roaming})
                {
                    nint memory=GetClipboardData(format); Require(memory!=0,"Required privacy format is absent.");
                    nint pointer=GlobalLock(memory); Require(pointer!=0,"Privacy data cannot be locked.");
                    try { Require(Marshal.ReadInt32(pointer)==0,"Temporary text permits history or roaming."); }
                    finally { GlobalUnlock(memory); }
                }
            }
            finally { CloseClipboard(); }
            var stale=TextClipboardPublisher.Publish(owner,unchecked(owned-1),"must not publish",default).GetAwaiter().GetResult();
            Require(stale is null && Native.GetClipboardSequenceNumber()==owned,"A stale writer changed newer clipboard content.");
            phase="restore disposable original";
            saved.Restore(owned).GetAwaiter().GetResult(); owned=0;
            phase="verify restored formats from a fresh consumer";
            // Verify a new app's view, independently of this process's former
            // OLE data object. Flush must make the formats self-contained.
            using(var consumer=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!,"--clipboard-publication-restored-check")
                { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true })
                ?? throw new InvalidOperationException("Cannot start the disposable clipboard consumer."))
            {
                if(!consumer.WaitForExit(5000)) { consumer.Kill(true); throw new TimeoutException("The clipboard consumer did not finish."); }
                Console.Write(consumer.StandardOutput.ReadToEnd());
                Require(consumer.ExitCode==0,"A fresh consumer could not read the original text/HTML.");
            }
            Console.WriteLine("PASS Immediate Unicode clipboard publication, history/roaming exclusion, stable sequence, stale-write rejection and rich-format restoration.");
            return 0;
        }
        catch(Exception error) { Console.WriteLine($"FAIL Clipboard publication fixture: phase={phase}; type={error.GetType().Name}; HRESULT=0x{error.HResult:X8}; {error.Message}"); return 1; }
        finally
        {
            try
            {
                // This opt-in fixture owns only disposable known data. Recover
                // its seed on a failed sequence assertion only while its window
                // is still the owner; never replace a different application's copy.
                if(saved is not null && owned!=0 && GetClipboardOwner()==owner)
                    saved.Restore(Native.GetClipboardSequenceNumber()).GetAwaiter().GetResult();
            }
            finally { saved?.Dispose(); if(owner!=0) DestroyWindow(owner); }
        }
    }
    internal static int CheckRestored()
    {
        try
        {
            var restored=Clipboard.GetContent();
            Require(restored.Contains(StandardDataFormats.Text) && restored.Contains(StandardDataFormats.Html),"Restored text or HTML is absent.");
            Require(restored.GetTextAsync().AsTask().GetAwaiter().GetResult()=="Vela publication original"
                && restored.GetHtmlFormatAsync().AsTask().GetAwaiter().GetResult().Contains("<b>Vela publication original</b>"),"Original text/HTML were not restored.");
            Console.WriteLine("PASS Fresh consumer reads restored text and HTML."); return 0;
        }
        catch(Exception error) { Console.WriteLine($"FAIL Fresh clipboard consumer: {error.GetType().Name}; HRESULT=0x{error.HResult:X8}; {error.Message}"); return 1; }
    }
    private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern nint CreateWindowExW(uint exStyle,string className,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(nint owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern nint GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern nint GetClipboardOwner();
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
}
