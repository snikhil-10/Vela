namespace Vela.Core;

public static class LinkCleaner
{
    private static readonly HashSet<string> Tracking = new(StringComparer.OrdinalIgnoreCase)
    { "fbclid", "gclid", "dclid", "msclkid", "mc_cid", "mc_eid", "igshid", "igsh", "twclid", "ttclid", "yclid", "_hsenc", "_hsmi", "vero_id", "oly_anon_id", "oly_enc_id" };

    // Leave opaque/signed query strings intact. Never decode/re-encode a retained parameter.
    public static string Clean(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return input;
        var text = input.Trim();
        int start = text.IndexOf('?');
        int fragment = text.IndexOf('#');
        if (start < 0 || fragment >= 0 && start > fragment) return input;
        int end = fragment >= 0 ? fragment : text.Length;
        var items = text[(start + 1)..end].Split('&');
        string Name(string item) { try { return Uri.UnescapeDataString(item.Split('=', 2)[0]); } catch (UriFormatException) { return item; } }
        if (items.Any(i => Name(i).Equals("X-Amz-Signature", StringComparison.OrdinalIgnoreCase)
            || Name(i).Equals("sig", StringComparison.OrdinalIgnoreCase)
            || Name(i).Equals("signature", StringComparison.OrdinalIgnoreCase))) return input;
        var kept = items.Where(i => { var name = Name(i); return !name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && !Tracking.Contains(name); }).ToArray();
        if (kept.Length == items.Length) return input;
        return text[..start] + (kept.Length > 0 ? "?" + string.Join('&', kept) : "") + text[end..];
    }
}
