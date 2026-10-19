using Microsoft.UI.Xaml;

namespace Vela.Windows;

public partial class App : Application
{
    private readonly bool startup;
    internal AppController? Controller { get; private set; }
    internal RegisteredWaitHandle? ActivationWait { get; set; }
    public App(bool startup)
    {
        this.startup = startup;
        UnhandledException+=(_,e)=>StartupDiagnostics.Record("XAML dispatcher: "+e.Message,e.Exception);
        DebugSettings.IsXamlResourceReferenceTracingEnabled=true;
        DebugSettings.XamlResourceReferenceFailed+=(_,e)=>StartupDiagnostics.Record("XAML resource",new InvalidOperationException(e.Message));
        try { InitializeComponent(); }
        catch(Exception error) { StartupDiagnostics.Record("Application resources",error); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Controller = new AppController();
            Controller.Initialize(startup);
        }
        catch(Exception error) { StartupDiagnostics.Record("Create application windows",error); throw; }
    }
    private bool quitting;
    internal async void Quit()
    {
        if(quitting) return; quitting=true;
        ActivationWait?.Unregister(null);
        if(Controller is not null) await Controller.FinishOperations();
        Controller?.Dispose();
        Exit();
    }
}
