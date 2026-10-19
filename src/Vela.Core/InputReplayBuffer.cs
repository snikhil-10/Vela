namespace Vela.Core;

public enum BufferedInputResult { Forward, Buffered, Full }

// Keeps physical input in order while an asynchronous text replacement prepares.
public sealed class InputReplayBuffer<T>
{
    public const int Capacity=4096;
    private readonly List<(uint Key,bool Down,T Item)> items=[];
    private readonly HashSet<uint> initiallyHeld=[];
    private readonly HashSet<uint> bufferedDown=[];
    private readonly HashSet<uint> replayedDown=[];
    private bool overflowed;
    public void Begin(IEnumerable<uint> heldModifiers)
    { items.Clear(); bufferedDown.Clear(); replayedDown.Clear(); overflowed=false; initiallyHeld.Clear(); initiallyHeld.UnionWith(heldModifiers); }
    public BufferedInputResult Capture(uint key,bool down,T item)
    {
        // Only releases of modifiers held before preparation can pass through.
        // A newly buffered Ctrl-down must retain its matching Ctrl-up.
        if(!down && initiallyHeld.Remove(key))
        {
            if(key is >=0xA0 and <=0xA5) initiallyHeld.Remove(0x10+(key-0xA0)/2);
            return BufferedInputResult.Forward;
        }
        if(items.Count>=Capacity) { overflowed=true; return BufferedInputResult.Full; }
        if(down) bufferedDown.Add(key); else bufferedDown.Remove(key);
        items.Add((key,down,item)); return BufferedInputResult.Buffered;
    }
    public bool TryTake(out T item)
    {
        if(items.Count==0) { item=default!; return false; }
        var next=items[0]; items.RemoveAt(0);
        if(next.Down) replayedDown.Add(next.Key); else replayedDown.Remove(next.Key);
        item=next.Item; return true;
    }
    // Cancellation drops unplayed input, but keys whose downs already reached
    // Windows need releases even if their ups are still waiting in the FIFO.
    public uint[] Discard()
    { var releases=replayedDown.ToArray(); Begin([]); return releases; }
    public T[] Drain(out uint[] releases)
    {
        var result=items.Select(entry=>entry.Item).ToArray(); releases=overflowed?bufferedDown.ToArray():[];
        Begin([]); return result;
    }
}
