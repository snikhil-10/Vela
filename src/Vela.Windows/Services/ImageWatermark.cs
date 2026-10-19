using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace Vela.Windows.Services;

internal sealed record ImageWatermark(string Text, double Opacity = .65, double SizePercent = 3, string Position = "Bottom right");

internal static class ImageWatermarks
{
    // GDI renders a small white-on-black coverage mask. Compositing that mask ourselves
    // preserves premultiplied alpha, including an otherwise transparent PNG background.
    public static void Apply(SoftwareBitmap bitmap, ImageWatermark options, CancellationToken cancellation)
    {
        string text = options.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length == 0) throw new ArgumentException("Enter watermark text before converting.");
        if (text.Length > 120) text = text[..120];
        int width = bitmap.PixelWidth, height = bitmap.PixelHeight;
        double opacity = double.IsFinite(options.Opacity) ? Math.Clamp(options.Opacity, .1, 1) : .65;
        double percent = double.IsFinite(options.SizePercent) ? Math.Clamp(options.SizePercent, 1, 10) : 3;
        int fontSize = Math.Clamp((int)Math.Round(Math.Max(width, height) * percent / 100), 8, 256);
        int padding = Math.Min(Math.Max(2, (int)(Math.Min(width, height) * .025)), Math.Max(0, Math.Min(width, height) / 4));
        int maskHeight = Math.Min(height, fontSize * 2 + padding * 2);
        bool top = options.Position.StartsWith("Top", StringComparison.Ordinal);
        bool left = options.Position.EndsWith("left", StringComparison.Ordinal);
        int offsetY = top ? 0 : height - maskHeight;
        byte[] mask = RenderMask(text, width, maskHeight, fontSize, padding, top, left);
        byte[] pixels = new byte[checked(width * height * 4)];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        for (int y = 0; y < maskHeight; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                int m = (y * width + x) * 4;
                int alpha = (int)Math.Round(mask[m] * opacity);
                if (alpha == 0) continue;
                int p = ((y + offsetY) * width + x) * 4;
                int remaining = 255 - alpha;
                for (int c = 0; c < 3; c++) pixels[p + c] = (byte)(alpha + (pixels[p + c] * remaining + 127) / 255);
                pixels[p + 3] = bitmap.BitmapAlphaMode == BitmapAlphaMode.Ignore ? (byte)255 : (byte)(alpha + (pixels[p + 3] * remaining + 127) / 255);
            }
        }
        bitmap.CopyFromBuffer(pixels.AsBuffer());
    }

    private static byte[] RenderMask(string text, int width, int height, int fontSize, int padding, bool top, bool left)
    {
        nint dc = WatermarkNative.CreateCompatibleDC(0), dib = 0, font = 0, oldBitmap = 0, oldFont = 0;
        if (dc == 0) throw new InvalidOperationException("Windows could not prepare the watermark.");
        try
        {
            var info = new WatermarkNative.BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            dib = WatermarkNative.CreateDIBSection(dc, ref info, 0, out nint bits, 0, 0);
            if (dib == 0 || bits == 0) throw new InvalidOperationException("Windows could not prepare the watermark bitmap.");
            oldBitmap = WatermarkNative.SelectObject(dc, dib);
            int length = checked(width * height * 4);
            byte[] mask = new byte[length]; Marshal.Copy(mask, 0, bits, length);
            font = WatermarkNative.CreateFont(-fontSize, 0, 0, 0, 600, 0, 0, 0, 1, 4, 0, 4, 0, "Segoe UI Variable");
            if (font == 0) throw new InvalidOperationException("Windows could not load the watermark font.");
            oldFont = WatermarkNative.SelectObject(dc, font);
            WatermarkNative.SetBkMode(dc, 1); WatermarkNative.SetTextColor(dc, 0xFFFFFF);
            var bounds = new WatermarkNative.Rect { Left = padding, Top = padding, Right = width - padding, Bottom = height - padding };
            // Single line, ellipsis, no mnemonic interpretation; align to the chosen corner.
            uint flags = 0x20u | 0x8000u | 0x800u | (left ? 0u : 2u) | (top ? 0u : 8u);
            if (WatermarkNative.DrawText(dc, text, text.Length, ref bounds, flags) == 0)
                throw new InvalidOperationException("The image is too small for a text watermark.");
            WatermarkNative.GdiFlush(); Marshal.Copy(bits, mask, 0, length); return mask;
        }
        finally
        {
            if (oldFont != 0) WatermarkNative.SelectObject(dc, oldFont);
            if (oldBitmap != 0) WatermarkNative.SelectObject(dc, oldBitmap);
            if (font != 0) WatermarkNative.DeleteObject(font);
            if (dib != 0) WatermarkNative.DeleteObject(dib);
            WatermarkNative.DeleteDC(dc);
        }
    }
}

internal static class WatermarkNative
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo
    { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPixels, YPixels; public uint Colors, Important, Color; }
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] internal static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll")] internal static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll")] internal static extern bool GdiFlush();
    [DllImport("gdi32.dll", EntryPoint = "CreateFontW", CharSet = CharSet.Unicode)]
    internal static extern nint CreateFont(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeout, uint charset, uint precision, uint clip, uint quality, uint pitch, string face);
    [DllImport("user32.dll", EntryPoint = "DrawTextW", CharSet = CharSet.Unicode)] internal static extern int DrawText(nint dc, string text, int length, ref Rect bounds, uint flags);
}
