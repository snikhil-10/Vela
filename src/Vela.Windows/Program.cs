using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.IO.MemoryMappedFiles;
using Vela.Windows.Interop;

namespace Vela.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch(Exception error) { StartupDiagnostics.Record("Application entry point",error); throw; }
    }
    private static void Run(string[] args)
    {
        AppDataPaths.Configure(args);
        WinRT.ComWrappersSupport.InitializeComWrappers();
        using var mutex = new Mutex(true, "Local\\Vela.Utilities.Desktop.Instance", out bool created);
        using var activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Vela.Utilities.Desktop.Activate");
        using var identity = MemoryMappedFile.CreateOrOpen("Local\\Vela.Utilities.Desktop.Identity",sizeof(int));
        using var identityView = identity.CreateViewAccessor();
        if (!created)
        {
            if (AppDataPaths.IsValidationProfile)
                throw new InvalidOperationException("Exit Vela before starting a separate validation profile.");
            // The process launched by Explorer can transfer its foreground activation
            // permission specifically to the resident process before signaling it.
            int processId=identityView.ReadInt32(0);
            if(processId>0) Native.AllowSetForegroundWindow((uint)processId);
            activation.Set(); return;
        }
        identityView.Write(0,Environment.ProcessId);
        Application.Start(_ =>
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
            var app = new App(args.Contains("--startup"), args.Contains("--settings"));
            app.ActivationWait = ThreadPool.RegisterWaitForSingleObject(activation,
                (_, _) => queue.TryEnqueue(() => app.Controller?.ShowPanel()), null, Timeout.Infinite, false);
        });
    }
}
