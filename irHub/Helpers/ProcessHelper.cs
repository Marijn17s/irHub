using System;
using System.Diagnostics;
using Serilog;

// ReSharper disable InconsistentNaming

namespace irHub.Helpers;

internal struct ProcessHelper
{
    /// <summary>
    /// Safely retrieve process name
    /// </summary>
    /// <param name="process"></param>
    /// <returns></returns>
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
}