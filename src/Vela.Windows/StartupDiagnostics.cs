namespace Vela.Windows;

internal static class StartupDiagnostics
{
    // A local failure report only; no telemetry or normal keystroke/activity logging.
    public static void Record(string stage,Exception error)
    {
        try
        {
            string directory=AppDataPaths.DirectoryPath;
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"startup-error.log");
            if(File.Exists(path) && new FileInfo(path).Length>1_048_576) File.WriteAllText(path,"");
            File.AppendAllText(path,$"{DateTimeOffset.Now:O}\n{stage}\n{error}\n\n");
        }
        catch { /* Diagnostics must never hide the original exception. */ }
    }
}
