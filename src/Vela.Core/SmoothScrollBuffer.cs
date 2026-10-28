namespace Vela.Core;

// Coalesces a notched-wheel gesture for one destination. The native wheel
// message carries a signed 16-bit delta, even when a burst queues more ticks.
public sealed class SmoothScrollBuffer
{
    private long remaining;
    private double fraction;
    public nint Target { get; private set; }
    public bool Pending=>remaining!=0;
    public void Add(nint target,int delta,double multiplier)
    {
        if(target==0 || !double.IsFinite(multiplier) || multiplier<=0) { Clear(); return; }
        if(Target!=target) Clear();
        Target=target;
        double scaled=delta*Math.Clamp(multiplier,.25,3)+fraction;
        long ticks=(long)Math.Round(scaled,MidpointRounding.AwayFromZero);
        fraction=scaled-ticks;
        remaining=Math.Clamp(remaining+ticks,int.MinValue,int.MaxValue);
    }
    public int TakeStep()
    {
        if(!Pending) return 0;
        int step=(int)Math.Clamp(Math.Round(remaining*.32),short.MinValue,short.MaxValue);
        if(step==0) step=Math.Sign(remaining);
        remaining-=step;
        return step;
    }
    public void Clear() { remaining=0; fraction=0; Target=0; }
}
