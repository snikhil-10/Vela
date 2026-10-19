using System.Globalization;

namespace Vela.Core;

public static class SnippetExpander
{
    public static string Expand(string text, DateTimeOffset now, string clipboard = "") => text
        .Replace("{date}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{time}", now.ToString("HH:mm", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{datetime}", now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{clipboard}", clipboard, StringComparison.Ordinal);
}
