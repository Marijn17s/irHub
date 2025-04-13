using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;
// ReSharper disable InconsistentNaming

namespace irHub.Helpers;

internal struct ApplicationWindowHelper
{
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint SC_MINIMIZE = 0xF020;
    
    internal static bool MinimizeWindowInterop(Process? process)
    {
        if (process is null || process.HasExited) return false;
        
        Log.Information($"Minimizing {process.ProcessName} to tray");
        
        var hWnd = process.MainWindowHandle;
        if (hWnd == IntPtr.Zero)
        {
            Log.Information($"Cannot find mainwindow handle for {process.ProcessName}");
            return false;
        }
        
        SendMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
        return true;
    }
    
    internal static bool CloseWindow(Process? process)
    {
        if (process is null || process.HasExited) return false;
        
        Log.Information($"Closing {process.ProcessName} to tray");
        return process.CloseMainWindow();
    }

    internal static bool CloseWindowInterop(Process? process)
    {
        if (process is null || process.HasExited) return false;

        Log.Information($"Closing {process.ProcessName} to tray using Interop");
        
        var hWnd = process.MainWindowHandle;
        if (hWnd == IntPtr.Zero)
        {
            Log.Information($"Cannot find mainwindow handle for {process.ProcessName}");
            return false;
        }
        
        SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        return true;
    }
    
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}