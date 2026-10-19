namespace Vela.Core;

public sealed class CleaningSession
{
    private long until, escapeSince = -1;
    private bool escapeCompleted;
    public bool Active { get; private set; }
    public bool LockMouse { get; private set; }
    public int SecondsRemaining(long now) => Active ? Math.Max(0, (int)Math.Ceiling((until - now) / 1000.0)) : 0;
    public void Start(long now, bool lockMouse) { until = now + 60_000; escapeSince = -1; escapeCompleted=false; LockMouse = lockMouse; Active = true; }
    public void Escape(bool down, long now)
    {
        if (!down) { if(escapeSince>=0 && now-escapeSince>=2_000) escapeCompleted=true; escapeSince = -1; }
        else if (escapeSince < 0) escapeSince = now;
    }
    public bool ShouldEnd(long now) => Active && (now >= until || escapeCompleted || escapeSince >= 0 && now - escapeSince >= 2_000);
    public void Stop() { Active = false; LockMouse = false; escapeSince = -1; escapeCompleted=false; }
}
