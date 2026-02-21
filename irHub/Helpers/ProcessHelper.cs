using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Serilog;

// ReSharper disable InconsistentNaming

namespace irHub.Helpers;

internal struct ProcessHelper
{
    /// <summary>
    /// Safely retrieve process name
    /// </summary>
    /// <param name="process"></param>
    /// <returns>The name of the given process</returns>
    internal static string GetProcessName(Process? process)
    {
        try
        {
            if (process is null || process.HasExited)
                return "Unknown process";
            return process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            Log.Warning("Process became invalid while retrieving process name");
            return "Unknown process";
        }
    }

    /// <summary>
    /// Gets the process id of the given process
    /// </summary>
    /// <param name="process">Process object</param>
    /// <returns>Process ID</returns>
    internal static int GetProcessId(Process? process)
    {
        try
        {
            if (process is null || process.HasExited)
                return 0;

            return Convert.ToInt32(process.Id);
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Avoid using, call is expensive
    /// </summary>
    /// <param name="name"></param>
    /// <returns>A list of processes that contain the given name</returns>
    internal static List<Process> GetProcessesByPartialName(string name)
    {
        Log.Information($"Attempting to retrieve processes with partial name: {name}");

        try
        {
            var processes = Process.GetProcesses()
                .Where(x => !x.HasExited && x.ProcessName.Contains(name, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            var processSpan = CollectionsMarshal.AsSpan(processes);
            foreach (var process in processSpan)
            {
                if (process is null || process.HasExited)
                    continue;
                Log.Information($"Found process {process.Id} with name {process.ProcessName}");
            }

            return processes;
        }
        catch (InvalidOperationException)
        {
            Log.Debug($"Process object is no longer valid when checking for partial name: {name}");
        }
        catch (Win32Exception win32Exception) when (win32Exception.Message is "Access is denied.")
        {
            // Get processes by full name as backup
            return Process.GetProcessesByName(name).ToList();
        }
        catch (Exception ex)
        {
            Log.Error($"Error while retrieving processes with partial name {name}: {ex.Message}");
        }
        
        return [];
    }

    /// <summary>
    /// Kill all processes that contain this name
    /// </summary>
    /// <param name="name"></param>
    internal static void KillProcessesByPartialName(string name)
    {
        Log.Information($"Attempting to kill processes with partial name: {name}..");
        var processes = GetProcessesByPartialName(name);
        
        var processSpan = CollectionsMarshal.AsSpan(processes);
        foreach (var process in processSpan)
        {
            try
            {
                Log.Information($"Killing process: {process.ProcessName}, process id: {process.Id}");
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process is no longer associated with a running process
                Log.Debug($"Process object is no longer valid when trying to kill processes with partial name: {name}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error while killing process with partial name {name}: {ex.Message}");
            }
        }
    }
}