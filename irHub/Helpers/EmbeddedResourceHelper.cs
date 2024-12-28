using System.IO;
using System.Reflection;
using Serilog;

namespace irHub.Helpers;

internal struct EmbeddedResourceHelper
{
    internal static string GetEmbeddedResource(string resourceName)
    {
        Log.Information($"Getting embedded resource {resourceName}");
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            Log.Error($"Embedded resource {resourceName} could not be found. Open an issue on GitHub here: https://github.com/Marijn17s/irHub/issues/new/choose");
            return "";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}