namespace Vela.Core;

public readonly record struct VideoBitrates(uint Video,uint Audio);

public static class VideoSizeBudget
{
    public const long MaximumBytes=2L*1024*1024*1024;
    public const int MaximumAttempts=3;
    private const uint MinimumVideo=64000,MinimumAudio=96000;

    // These are conservative starting rates, not a size guarantee. The caller
    // must measure the completed container and discard any oversized copy.
    public static VideoBitrates? Plan(long maximumBytes,TimeSpan duration,uint video,uint audio)
    {
        Validate(maximumBytes);
        if(duration<=TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if(video==0) throw new ArgumentOutOfRangeException(nameof(video));
        if(audio is >0 and <MinimumAudio) throw new ArgumentOutOfRangeException(nameof(audio));
        long payload=maximumBytes-Math.Max(4096,maximumBytes/10);
        if(payload<=0) return null;
        double total=Math.Floor(payload*8d/duration.TotalSeconds);
        uint minimumVideo=Math.Min(video,MinimumVideo),minimumAudio=Math.Min(audio,MinimumAudio);
        if(total<(double)minimumVideo+minimumAudio) return null;
        uint plannedAudio=audio==0?0:AudioSizeBudget.SelectRate(audio,total/5);
        uint plannedVideo=(uint)Math.Min(video,total-plannedAudio);
        return new(plannedVideo,plannedAudio);
    }

    public static VideoBitrates? Lower(VideoBitrates current,long maximumBytes,long actualBytes)
    {
        Validate(maximumBytes);
        if(actualBytes<=maximumBytes || current.Video==0) throw new ArgumentOutOfRangeException(nameof(actualBytes));
        if(current.Audio is >0 and <MinimumAudio) throw new ArgumentOutOfRangeException(nameof(current));
        double ratio=maximumBytes/(double)actualBytes*.85;
        uint video=(uint)Math.Min(current.Video,Math.Max(MinimumVideo,Math.Floor(current.Video*ratio)));
        uint audio=current.Audio==0 ? 0 : AudioSizeBudget.SelectRate(current.Audio,current.Audio*ratio);
        return video==current.Video && audio==current.Audio ? null : new(video,audio);
    }

    private static void Validate(long maximumBytes)
    {
        if(maximumBytes is <=0 or >MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
    }
}
