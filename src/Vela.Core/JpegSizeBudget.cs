namespace Vela.Core;

public readonly record struct JpegQualityChoice(int Quality,long Bytes,int Attempts);

// Select a tested quality that fits. The final encoder must still enforce the
// byte limit: encoder size is not a universal monotonicity or determinism contract.
public static class JpegSizeBudget
{
    public static async Task<JpegQualityChoice?> Choose(long maximumBytes,int preferredQuality,
        Func<int,CancellationToken,Task<long>> measure,CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if(maximumBytes<=0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if(preferredQuality is <10 or >100) throw new ArgumentOutOfRangeException(nameof(preferredQuality));
        int attempts=0;
        async Task<long> Probe(int quality)
        {
            cancellation.ThrowIfCancellationRequested();
            long bytes=await measure(quality,cancellation); attempts++;
            cancellation.ThrowIfCancellationRequested();
            if(bytes<=0) throw new InvalidDataException("The JPEG encoder produced no image.");
            return bytes;
        }
        long upper=await Probe(preferredQuality);
        if(upper<=maximumBytes) return new(preferredQuality,upper,attempts);
        if(preferredQuality==10) return null;
        long lower=await Probe(10);
        if(lower>maximumBytes) return null;
        int best=10,low=11,high=preferredQuality-1;
        long bestBytes=lower;
        while(low<=high)
        {
            int candidate=low+(high-low)/2;
            long bytes=await Probe(candidate);
            if(bytes<=maximumBytes) { best=candidate; bestBytes=bytes; low=candidate+1; }
            else high=candidate-1;
        }
        return new(best,bestBytes,attempts);
    }
}
