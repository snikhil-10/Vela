namespace Vela.Windows;

// Explicit local validation profiles keep development fixtures out of normal user data.
internal static class AppDataPaths
{
    private static readonly string NormalDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vela");
    internal static string DirectoryPath { get; private set; } = NormalDirectory;
    internal static bool IsValidationProfile { get; private set; }
    internal static void Configure(string[] args)
    {
        int index = Array.IndexOf(args, "--data-directory");
        if (index < 0) return;
        if (index + 1 >= args.Length || !Path.IsPathFullyQualified(args[index + 1])
            || args.Count(arg => arg == "--data-directory") != 1)
            throw new ArgumentException("--data-directory requires one absolute local directory path.");
        string path = Path.GetFullPath(args[index + 1]);
        if (path.StartsWith("\\\\", StringComparison.Ordinal) || string.Equals(
            Path.TrimEndingDirectorySeparator(path), Path.TrimEndingDirectorySeparator(NormalDirectory), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A validation profile requires a separate directory on a local drive.");
        DirectoryPath = path;
        IsValidationProfile = true;
    }
}
