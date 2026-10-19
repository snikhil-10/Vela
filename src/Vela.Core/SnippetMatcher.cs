using System.Globalization;

namespace Vela.Core;

public sealed record SnippetMatch(Snippet Snippet, int DeleteCount, string Completion, string Delimiter);

public sealed class SnippetMatcher
{
    private string buffer = "";
    public static bool ValidTrigger(string? value) => value is { Length: > 0 and <= 64 }
        && !value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
    public void Clear() => buffer = "";
    public void Backspace()
    {
        var positions = StringInfo.ParseCombiningCharacters(buffer);
        buffer = positions.Length == 0 ? "" : buffer[..positions[^1]];
    }
    public SnippetMatch? Push(string text, IEnumerable<Snippet> snippets)
    {
        if (text.Length == 0) return null;
        bool delimiter = text is " " or "\t" or "\r" or "\n";
        if (!delimiter && text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) { Clear(); return null; }
        string candidate = delimiter ? buffer : buffer + text;
        var match = snippets.Where(s => ValidTrigger(s.Trigger)
                && s.Expansion == (delimiter ? "afterDelimiter" : "immediate")
                && candidate.EndsWith(s.Trigger, StringComparison.Ordinal))
            .OrderByDescending(s => s.Trigger.Length).FirstOrDefault();
        if (match is not null)
        {
            string alreadyTyped = delimiter ? match.Trigger : match.Trigger[..Math.Max(0, match.Trigger.Length - text.Length)];
            Clear();
            // The completing key is swallowed, so only erase characters already delivered.
            return new(match, StringInfo.ParseCombiningCharacters(alreadyTyped).Length, text, delimiter ? text : "");
        }
        buffer = delimiter ? "" : candidate.Length > 96 ? candidate[^96..] : candidate;
        return null;
    }
}
