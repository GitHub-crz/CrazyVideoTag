using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CrazyVideoTag.Services;

public static class WindowThemeHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyDarkTheme(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var useDarkMode = 1;
        DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));

        var captionColor = ToColorRef(0x15, 0x1A, 0x23);
        DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColor, sizeof(int));

        var borderColor = ToColorRef(0x0D, 0x0F, 0x14);
        DwmSetWindowAttribute(handle, DwmwaBorderColor, ref borderColor, sizeof(int));
    }

    private static int ToColorRef(byte red, byte green, byte blue) => red | (green << 8) | (blue << 16);
}
