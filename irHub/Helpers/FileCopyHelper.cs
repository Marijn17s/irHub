using System.IO;
using System.Threading.Tasks;
using irHub.Classes;
using Serilog;

namespace irHub.Helpers;

internal struct FileCopyHelper
{
    internal static string CopyImage(string source)
    {
        Log.Information($"Copying image from path: {source}");
        
        var fileName = Path.GetFileName(source);
        var destinationPath = Path.Combine(Global.irHubDirectoryPath, "resources\\images");
        var destination = Path.Combine(destinationPath, fileName);
        
        Directory.CreateDirectory(destinationPath);
        File.Copy(source, destination, true);
        return destination;
    }
    
    internal static async Task<string> CopyImageAsync(string source)
    {
        Log.Information($"Asyncronously copying image from path: {source}");
        
        var fileName = Path.GetFileName(source);
        var destinationPath = Path.Combine(Global.irHubDirectoryPath, "resources\\images");
        var destination = Path.Combine(destinationPath, fileName);
        
        Directory.CreateDirectory(destinationPath);

        await using var sourceStream = File.OpenRead(source);
        await using var destinationStream = File.Create(destination);
        await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);
        
        return destination;
    }
    
    internal static string CopyMisc(string source)
    {
        Log.Information($"Copying misc from path: {source}");
        
        var fileName = Path.GetFileName(source);
        var destinationPath = Path.Combine(Global.irHubDirectoryPath, "resources\\misc");
        var destination = Path.Combine(destinationPath, fileName);
        
        Directory.CreateDirectory(destinationPath);
        File.Copy(source, destination, true);
        return destination;
    }
    
    internal static async Task<string> CopyMiscAsync(string source)
    {
        Log.Information($"Asyncronously copying misc from path: {source}");
        
        var fileName = Path.GetFileName(source);
        var destinationPath = Path.Combine(Global.irHubDirectoryPath, "resources\\misc");
        var destination = Path.Combine(destinationPath, fileName);
        
        Directory.CreateDirectory(destinationPath);

        await using var sourceStream = File.OpenRead(source);
        await using var destinationStream = File.Create(destination);
        await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);
        
        return destination;
    }
}