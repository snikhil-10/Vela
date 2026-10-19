using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Vela.Windows.Interop;

// The existing multi-resolution Vela artwork supplies the alpha mask. Tinting
// keeps its antialiasing and transparent background at the shell's actual DPI.
internal static class ThemedIcon
{
    internal static bool ShellIsLight
    {
        get
        {
            try
            {
                using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if(key?.GetValue("SystemUsesLightTheme") is int light) return light!=0;
            }
            catch(Exception error) when(error is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
            var color=new UISettings().GetColorValue(UIColorType.Foreground);
            return color.R*299+color.G*587+color.B*114<128000;
        }
    }

    internal static Color Foreground(bool light)
    {
        if(!new AccessibilitySettings().HighContrast)
            return light ? Color.FromArgb(255,32,32,32) : Color.FromArgb(255,245,245,245);
        uint color=GetSysColor(8); // COLOR_WINDOWTEXT belongs to the active contrast palette.
        return Color.FromArgb(255,(byte)color,(byte)(color>>8),(byte)(color>>16));
    }

    internal static nint Create(int size,Color color)
    {
        nint source=Native.LoadImage(0,Path.Combine(AppContext.BaseDirectory,"Assets","Vela.ico"),1,size,size,0x10);
        if(source==0) throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not load Vela's icon.");
        IconInfo original=default;
        nint dc=0,bitmap=0;
        try
        {
            if(!GetIconInfo(source,out original)) throw new Win32Exception();
            dc=CreateCompatibleDC(0);
            if(dc==0) throw new Win32Exception();
            var info=new BitmapInfo { Size=40,Width=size,Height=-size,Planes=1,BitCount=32,ImageSize=(uint)(size*size*4) };
            var pixels=new byte[size*size*4];
            if(GetDIBits(dc,original.Color,0,(uint)size,pixels,ref info,0)!=size) throw new Win32Exception();
            for(int i=0;i<pixels.Length;i+=4)
            {
                byte alpha=pixels[i+3];
                pixels[i]=(byte)(color.B*alpha/255); pixels[i+1]=(byte)(color.G*alpha/255); pixels[i+2]=(byte)(color.R*alpha/255);
            }
            bitmap=CreateDIBSection(dc,ref info,0,out nint bits,0,0);
            if(bitmap==0 || bits==0) throw new Win32Exception();
            Marshal.Copy(pixels,0,bits,pixels.Length);
            var tinted=new IconInfo { IsIcon=true,Mask=original.Mask,Color=bitmap };
            nint icon=CreateIconIndirect(ref tinted);
            if(icon==0) throw new Win32Exception();
            return icon;
        }
        finally
        {
            if(bitmap!=0) Native.DeleteObject(bitmap);
            if(original.Color!=0) Native.DeleteObject(original.Color);
            if(original.Mask!=0) Native.DeleteObject(original.Mask);
            if(dc!=0) DeleteDC(dc);
            Native.DestroyIcon(source);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct IconInfo
    { [MarshalAs(UnmanagedType.Bool)] public bool IsIcon; public uint XHotspot,YHotspot; public nint Mask,Color; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width,Height; public ushort Planes,BitCount;
        public uint Compression,ImageSize; public int XPelsPerMeter,YPelsPerMeter; public uint ColorsUsed,ColorsImportant,Color;
    }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetIconInfo(nint icon,out IconInfo info);
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
    [DllImport("user32.dll",SetLastError=true)] private static extern nint CreateIconIndirect(ref IconInfo info);
    [DllImport("gdi32.dll",SetLastError=true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll",SetLastError=true)] private static extern int GetDIBits(nint dc,nint bitmap,uint start,uint count,[Out] byte[] pixels,ref BitmapInfo info,uint usage);
    [DllImport("gdi32.dll",SetLastError=true)] private static extern nint CreateDIBSection(nint dc,ref BitmapInfo info,uint usage,out nint bits,nint section,uint offset);
}
