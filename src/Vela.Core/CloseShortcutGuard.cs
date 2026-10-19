namespace Vela.Core;

public sealed class CloseShortcutGuard
{
    private readonly Dictionary<string,long> previous=[];
    public bool Allow(string action,long milliseconds,bool repeat)
    {
        if(repeat) return false;
        if(previous.Remove(action,out long time) && milliseconds-time is >=0 and <=1200) return true;
        previous[action]=milliseconds;
        return false;
    }
    public void Clear()=>previous.Clear();
}
