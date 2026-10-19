namespace Vela.Core;

// One instance per metric/device. Timestamps must come from the monotonic sampling clock.
public sealed class SustainedAlertGate(long holdMilliseconds=30_000, long cooldownMilliseconds=900_000,
    long maximumSampleGapMilliseconds=12_000, bool below=false)
{
    private long? heldSince, lastReadingAt, lastAlertAt;
    private double? observedThreshold;

    public bool Observe(double? reading, double threshold, long readAt)
    {
        if(reading is not {} value || !double.IsFinite(value) || !double.IsFinite(threshold) ||
            (below ? value>=threshold : value<threshold))
        { ResetObservation(); return false; }

        if(observedThreshold!=threshold || lastReadingAt is {} previous &&
            (readAt<previous || readAt-previous>maximumSampleGapMilliseconds)) ResetObservation();
        if(lastReadingAt==readAt) return false;
        observedThreshold=threshold; lastReadingAt=readAt; heldSince??=readAt;
        if(readAt-heldSince.Value<holdMilliseconds ||
            lastAlertAt is {} alerted && readAt-alerted<cooldownMilliseconds) return false;
        lastAlertAt=readAt;
        return true;
    }

    // A sampling interruption must not erase the notification cooldown.
    public void ResetObservation() { heldSince=null; lastReadingAt=null; observedThreshold=null; }
}
