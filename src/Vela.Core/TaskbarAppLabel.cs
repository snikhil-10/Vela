namespace Vela.Core;

public static class TaskbarAppLabel
{
    private static readonly string[] Labels=["Vela","Vela.Windows","Vela.Windows.exe","Vela · File shelf"];
    // Explorer may expose the executable's description/name instead of a
    // window title, and append a running-window count for a grouped button.
    public static bool IsVela(string? value)
    {
        if(string.IsNullOrWhiteSpace(value)) return false;
        string name=value.Trim();
        foreach(string label in Labels)
        {
            if(name.Equals(label,StringComparison.OrdinalIgnoreCase)) return true;
            if(!name.StartsWith(label,StringComparison.OrdinalIgnoreCase)) continue;
            string suffix=name[label.Length..];
            if(label=="Vela · File shelf" && suffix.StartsWith(" - ",StringComparison.Ordinal)) return true;
            // Do not mistake similarly named apps or document titles for Vela.
            if(suffix.StartsWith(", ",StringComparison.Ordinal)) suffix=suffix[2..];
            else if(suffix.StartsWith(" - ",StringComparison.Ordinal)) suffix=suffix[3..];
            else if(suffix.StartsWith(" (",StringComparison.Ordinal) && suffix.EndsWith(')')) suffix=suffix[2..^1];
            else continue;
            int digits=0;
            while(digits<suffix.Length && char.IsDigit(suffix[digits])) digits++;
            if(digits>0 && (digits==suffix.Length || char.IsWhiteSpace(suffix[digits]))) return true;
        }
        return false;
    }
}

public static class TaskbarDropGeometry
{
    public static (int X,int Y) ButtonProbe(int left,int top,int right,int bottom,int x,int y)
    {
        if(right<=left || bottom<=top) throw new ArgumentOutOfRangeException(nameof(right));
        // A point one pixel inside the edge hits taskbar padding rather than
        // its button. Probe the center band before the drag arrives there.
        return right-left>=bottom-top
            ? (Math.Clamp(x,left,right-1),top+(bottom-top)/2)
            : (left+(right-left)/2,Math.Clamp(y,top,bottom-1));
    }
}
