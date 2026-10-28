namespace Vela.Core;

public enum AwakeReason { Off, Timer, Application, ExternalDisplay, Battery, PowerUnavailable }
public readonly record struct AwakeDecision(bool Requested,AwakeReason Reason,TimeSpan Remaining);

// Calendar changes must not lengthen or shorten a transient wake request.
// Battery pauses the request, while its elapsed-time deadline keeps running.
public sealed class AwakeState(TimeProvider? clock=null)
{
    private readonly TimeProvider clock=clock??TimeProvider.System;
    private long started;
    private TimeSpan duration;
    public bool IsActive { get; private set; }
    public TimeSpan Remaining
    {
        get
        {
            if(duration==TimeSpan.Zero) return TimeSpan.Zero;
            var elapsed=clock.GetElapsedTime(started);
            if(elapsed<TimeSpan.Zero || elapsed>=duration) { Release(); return TimeSpan.Zero; }
            return duration-elapsed;
        }
    }
    public void Start()=>IsActive=true;
    public void Stop() { IsActive=false; Release(); }
    public void Release()=>duration=TimeSpan.Zero;
    public bool Hold(TimeSpan value)
    {
        if(!IsActive) return false;
        if(value<=TimeSpan.Zero || value>TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(value),"Choose a duration greater than zero and at most 24 hours.");
        started=clock.GetTimestamp(); duration=value; return true;
    }
    public AwakeDecision Decide(bool onlyOnAc,bool? pluggedIn,bool appRule,bool externalDisplayRule)
    {
        var remaining=Remaining;
        if(!IsActive || remaining==TimeSpan.Zero && !appRule && !externalDisplayRule) return new(false,AwakeReason.Off,remaining);
        if(onlyOnAc && pluggedIn!=true) return new(false,pluggedIn is false?AwakeReason.Battery:AwakeReason.PowerUnavailable,remaining);
        return new(true,remaining>TimeSpan.Zero?AwakeReason.Timer:externalDisplayRule?AwakeReason.ExternalDisplay:AwakeReason.Application,remaining);
    }
}
