namespace Vela.Core;

public static class AudioSizeBudget
{
    public const long MaximumBytes=2L*1024*1024*1024;
    public const int MaximumAttempts=3;
    public const uint MinimumBitrate=96000;
    // Microsoft Media Foundation AAC mono/stereo output rates, in bits/second.
    private static readonly uint[] Rates=[96000,128000,160000,192000];

    // The estimate reserves container space. Actual output bytes must still be
    // measured by the encoder's caller before retaining any completed copy.
    public static uint? Plan(long maximumBytes,TimeSpan duration,uint maximumBitrate)
    {
        Validate(maximumBytes,maximumBitrate);
        if(duration<=TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        long payload=maximumBytes-Math.Max(4096,maximumBytes/10);
        if(payload<=0) return null;
        double desired=Math.Floor(payload*8d/duration.TotalSeconds);
        return desired<MinimumBitrate ? null : SelectRate(maximumBitrate,desired);
    }

    public static uint? Lower(uint current,long maximumBytes,long actualBytes)
    {
        Validate(maximumBytes,current);
        if(actualBytes<=maximumBytes) throw new ArgumentOutOfRangeException(nameof(actualBytes));
        uint next=SelectRate(current,current*(maximumBytes/(double)actualBytes)*.85);
        return next<current ? next : null;
    }

    internal static uint SelectRate(uint ceiling,double desired)=>Rates.Last(rate=>rate<=ceiling && rate<=Math.Max(MinimumBitrate,desired));

    private static void Validate(long maximumBytes,uint bitrate)
    {
        if(maximumBytes is <=0 or >MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if(bitrate<MinimumBitrate) throw new ArgumentOutOfRangeException(nameof(bitrate));
    }
}
