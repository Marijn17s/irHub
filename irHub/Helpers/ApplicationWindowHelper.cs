using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using irHub.Classes.Enums;
using Serilog;

// ReSharper disable InconsistentNaming

namespace irHub.Helpers;

internal struct ApplicationWindowHelper
{
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint SC_MINIMIZE = 0xF020;
    private const int CHECK_INTERVAL_MS = 100;
    internal const int WINDOW_TIMEOUT_MS = 10000;
    
    private static bool MinimizeWindowInterop(Process? process)
    {
        if (process is null || process.HasExited) return false;
        
        Log.Information($"Minimizing {ProcessHelper.GetProcessName(process)} to tray");
        
        var hWnd = process.MainWindowHandle;
        if (hWnd == IntPtr.Zero)
        {
            Log.Information($"Cannot find mainwindow handle for {ProcessHelper.GetProcessName(process)}");
            return false;
        }
        
        SendMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
        return true;
    }
    
    private static bool CloseWindow(Process? process)
    {
        if (process is null || process.HasExited)
        {
            Log.Warning($"Process {ProcessHelper.GetProcessName(process)} became invalid before closing window");
            return false;
        }
        
        Log.Information($"Closing {ProcessHelper.GetProcessName(process)} to tray");
        return process.CloseMainWindow();
    }

    private static bool CloseWindowInterop(Process? process)
    {
        if (process is null || process.HasExited)
        {
            Log.Warning($"Process {ProcessHelper.GetProcessName(process)} became invalid before closing window with interop");
            return false;
        }

        Log.Information($"Closing {ProcessHelper.GetProcessName(process)} to tray using Interop");
        
        var hWnd = process.MainWindowHandle;
        if (hWnd == IntPtr.Zero)
        {
            Log.Information($"Cannot find mainwindow handle for {ProcessHelper.GetProcessName(process)}");
            return false;
        }
        
        SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        return true;
    }
    
    private static async Task<bool> WaitForWindowAsync(Process? process)
    {
        if (process is null || process.HasExited) return false;

        Log.Information($"Waiting for window to become available for {ProcessHelper.GetProcessName(process)} (timeout: {WINDOW_TIMEOUT_MS}ms)");
        
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(WINDOW_TIMEOUT_MS);
        
        while (DateTime.Now - startTime < timeout)
        {
            try
            {
                // Refresh the process to get updated information
                process.Refresh();
                
                if (process.HasExited)
                {
                    Log.Warning($"Process {ProcessHelper.GetProcessName(process)} exited while waiting for window");
                    return false;
                }
                
                var hWnd = process.MainWindowHandle;
                if (hWnd != IntPtr.Zero && IsWindowVisible(hWnd))
                {
                    Log.Information($"Window became available for {ProcessHelper.GetProcessName(process)} after {(DateTime.Now - startTime).TotalMilliseconds:F0}ms");
                    return true;
                }
                
                // Wait before next check
                await Task.Delay(CHECK_INTERVAL_MS);
            }
            catch (InvalidOperationException)
            {
                Log.Warning($"Process {ProcessHelper.GetProcessName(process)} became invalid while waiting for window");
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning($"Error while waiting for window for {ProcessHelper.GetProcessName(process)}: {ex.Message}");
                await Task.Delay(CHECK_INTERVAL_MS);
            }
        }
        
        Log.Warning($"Timeout waiting for window for {ProcessHelper.GetProcessName(process)} after {WINDOW_TIMEOUT_MS}ms");
        return false;
    }
    
    internal static async Task<bool> WaitAndOperateOnWindowAsync(Process? process, WindowOperation operation)
    {
        if (process is null || process.HasExited)
        {
            Log.Warning($"Process {ProcessHelper.GetProcessName(process)} became invalid before operating on window");
            return false;
        }

        Log.Information($"Waiting for window and performing {operation} operation for {ProcessHelper.GetProcessName(process)}");
        
        // Wait for window to become available
        var windowAvailable = await WaitForWindowAsync(process);
        if (windowAvailable)
            return operation switch
            {
                WindowOperation.Minimize => MinimizeWindowInterop(process),
                WindowOperation.Close => CloseWindow(process) || CloseWindowInterop(process),
                _ => false
            };
        Log.Warning($"Window did not become available for {ProcessHelper.GetProcessName(process)}, cannot perform {operation} operation");
        return false;
    }
    
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}