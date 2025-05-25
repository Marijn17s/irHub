using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using Serilog;

namespace irHub.Helpers;

internal static class StartupHelper
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApplicationName = "irHub";
    
    internal static void EnableStartup()
    {
        // Add application to Windows startup registry
        try
        {
            var executablePath = GetExecutablePath();
            if (string.IsNullOrEmpty(executablePath))
            {
                Log.Warning("Could not determine executable path for startup registration");
                return;
            }
            
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key is null)
            {
                Log.Error("Could not open Windows startup registry key");
                return;
            }
            
            var startupCommand = $"\"{executablePath}\" --minimized";
                
            key.SetValue(ApplicationName, startupCommand);
            Log.Information($"Successfully enabled startup with Windows: {startupCommand}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to enable startup with Windows: {ex.Message}");
        }
    }
    
    internal static void DisableStartup()
    {
        // Remove application from Windows startup registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key is null)
            {
                Log.Error("Could not open Windows startup registry key");
                return;
            }
            
            if (key.GetValue(ApplicationName) is not null)
            {
                key.DeleteValue(ApplicationName);
                Log.Information("Successfully disabled startup with Windows");
            }
            else Log.Debug("Startup registry entry was already removed or never existed");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to disable startup with Windows: {ex.Message}");
        }
    }
    
    internal static bool IsStartupEnabled()
    {
        // Check if application is registered for Windows startup
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            if (key is null)
            {
                Log.Debug("Could not open Windows startup registry key for reading");
                return false;
            }
            
            var value = key.GetValue(ApplicationName);
            var isEnabled = value is not null;
            
            Log.Debug($"Startup with Windows is {(isEnabled ? "enabled" : "disabled")}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to check startup status: {ex.Message}");
            return false;
        }
    }
    
    private static string GetExecutablePath()
    {
        // Get the current executable path for registry entry
        try
        {
            var mainModule = Process.GetCurrentProcess().MainModule;
            if (mainModule?.FileName is not null)
            {
                return mainModule.FileName;
            }
            
            var assembly = Assembly.GetExecutingAssembly();
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                return assembly.Location;
            }
            
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly?.Location is not null)
                return entryAssembly.Location;
            
            Log.Warning("Could not determine executable path using any method");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error($"Error getting executable path: {ex.Message}");
            return string.Empty;
        }
    }
} 