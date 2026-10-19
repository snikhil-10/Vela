namespace Vela.Core;

public readonly record struct Shortcut(uint Modifiers, uint Key)
{
    public static bool TryParse(string? text, out Shortcut result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            uint flag = part.ToLowerInvariant() switch { "alt" => 1, "ctrl" => 2, "shift" => 4, "win" => 8, _ => 0 };
            if (flag == 0 || (modifiers & flag) != 0) return false;
            modifiers |= flag;
        }
        var keyName = parts[^1].ToUpperInvariant();
        uint key = keyName switch { "SPACE" => 0x20, "ESC" => 0x1B, "TAB" => 9, "ENTER" => 13, _ => 0 };
        if (keyName.Length == 1 && char.IsAsciiLetterOrDigit(keyName[0])) key = keyName[0];
        if (keyName.StartsWith('F') && int.TryParse(keyName[1..], out int n) && n is >= 1 and <= 24) key = (uint)(0x6F + n);
        if (key == 0) return false;
        result = new(modifiers, key);
        return true;
    }
}
