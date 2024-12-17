using System.IO;
using System.Reflection;

namespace irHub.Helpers;

internal struct EmbeddedResourceHelper
{
    internal static string GetEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return "";

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}