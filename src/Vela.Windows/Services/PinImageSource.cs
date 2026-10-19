using Vela.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Vela.Windows.Services;

internal static class PinImageSource
{
    public const uint PreviewEdge = 2048;
    public static async Task<SoftwareBitmap> Decode(IRandomAccessStream stream, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(stream.Size>128UL*1024*1024) throw new InvalidOperationException("Choose an image smaller than 128 MB.");
        var decoder=await BitmapDecoder.CreateAsync(stream).AsTask(cancellation);
        if((long)decoder.PixelWidth*decoder.PixelHeight>100_000_000)
            throw new InvalidOperationException("Choose an image smaller than 100 megapixels.");
        var size=ImageSizing.Fit(decoder.PixelWidth,decoder.PixelHeight,PreviewEdge);
        var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth=size.Width,ScaledHeight=size.Height,InterpolationMode=BitmapInterpolationMode.Fant },
            ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(cancellation);
        if(cancellation.IsCancellationRequested) { bitmap.Dispose(); cancellation.ThrowIfCancellationRequested(); }
        return bitmap;
    }
}
