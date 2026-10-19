namespace Vela.Core;

// A replayed completing key and a newly typed physical key can coexist while
// the next replacement waits for the clipboard. Their releases are independent.
public sealed class SnippetKeyGuard
{
    private readonly HashSet<uint> physical=[], replayed=[];
    public void Complete(uint key,bool replay) => (replay?replayed:physical).Add(key);
    public bool Consume(uint key,bool down,bool replay)
    {
        var held=replay?replayed:physical;
        if(!held.Contains(key)) return false;
        if(!down) held.Remove(key);
        return true;
    }
    public void DropReplay() => replayed.Clear();
    public void Clear() { physical.Clear(); replayed.Clear(); }
}
