using Microsoft.Win32;

namespace Vela.Windows.Services;

internal static class StartupService
{
    private const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    public static bool Enabled
    {
        get { using var key=Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("Vela.Utilities") is string; }
        set
        {
            using var key=Registry.CurrentUser.CreateSubKey(RunKey);
            if(value) key.SetValue("Vela.Utilities",$"\"{Environment.ProcessPath}\" --startup");
            else key.DeleteValue("Vela.Utilities",false);
        }
    }
}
