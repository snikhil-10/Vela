namespace Vela.Core;

public static class ImageSizing
{
    public static (uint Width,uint Height) Fit(uint width,uint height,uint longestEdge)
    {
        if(width==0 || height==0) throw new ArgumentOutOfRangeException(nameof(width));
        if(longestEdge==0 || Math.Max(width,height)<=longestEdge) return (width,height);
        double ratio=(double)longestEdge/Math.Max(width,height);
        return (Math.Max(1,(uint)Math.Round(width*ratio)),Math.Max(1,(uint)Math.Round(height*ratio)));
    }
}
