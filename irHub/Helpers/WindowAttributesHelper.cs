using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace irHub.Helpers;

internal struct WindowAttributesHelper
{
    // 2 = Rounded, 1 = Default, 0 = No Rounding
    
    internal static void SetRoundedCorners(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        const int dwmwaWindowCornerPreference = 33;
        int dwmWindowCornerRound = 2;

        _ = DwmSetWindowAttribute(hwnd, dwmwaWindowCornerPreference, ref dwmWindowCornerRound, sizeof(int));
    }
    
    internal static void SetRoundedCorners(HandyControl.Controls.Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        const int dwmwaWindowCornerPreference = 33;
        int dwmWindowCornerRound = 2;

        _ = DwmSetWindowAttribute(hwnd, dwmwaWindowCornerPreference, ref dwmWindowCornerRound, sizeof(int));
    }
    
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}