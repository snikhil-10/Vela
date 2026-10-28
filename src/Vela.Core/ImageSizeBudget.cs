namespace Vela.Core;

public readonly record struct ImageSizeChoice(uint LongestEdge,long Bytes,int Attempts);

// PNG/BMP retain their format and shrink dimensions only when explicitly given
// a byte budget. Select a measured candidate; encoded size need not be monotonic.
public static class ImageSizeBudget
{
    public const uint MinimumEdge=64;
    public const int MaximumAttempts=12;
    public static async Task<ImageSizeChoice?> Choose(long maximumBytes,uint longestEdge,
        Func<uint,CancellationToken,Task<long>> measure,CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if(maximumBytes<=0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if(longestEdge==0) throw new ArgumentOutOfRangeException(nameof(longestEdge));
        int attempts=0;
        async Task<long> Probe(uint edge)
        {
            cancellation.ThrowIfCancellationRequested();
            long bytes=await measure(edge,cancellation); attempts++;
            cancellation.ThrowIfCancellationRequested();
            if(bytes<=0) throw new InvalidDataException("The image encoder produced no image.");
            return bytes;
        }
        long original=await Probe(longestEdge);
        if(original<=maximumBytes) return new(longestEdge,original,attempts);
        uint minimum=Math.Min(MinimumEdge,longestEdge);
        if(minimum==longestEdge) return null;
        long smallest=await Probe(minimum);
        if(smallest>maximumBytes) return null;
        uint best=minimum,low=minimum+1,high=longestEdge-1;
        long bestBytes=smallest;
        while(low<=high && attempts<MaximumAttempts)
        {
            uint candidate=low+(high-low)/2;
            long bytes=await Probe(candidate);
            if(bytes<=maximumBytes) { best=candidate; bestBytes=bytes; low=candidate+1; }
            else high=candidate-1;
        }
        return new(best,bestBytes,attempts);
    }
}
