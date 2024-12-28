using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using irHub.Classes;
using Serilog;
using Image = System.Windows.Controls.Image;
using Path = System.IO.Path;

namespace irHub.Helpers;

internal struct IconHelper
{
    internal static Image GetIconFromFile(string path)
    {
        Log.Information($"Loading icon from {path}");
        if (!File.Exists(path) || !Global.IsFile(path))
        {
            Log.Warning("File does not exist or access to file is denied - GetIconFromFile");
            return Global.DefaultIcon;
        }
        
        var fileExtension = Path.GetExtension(path);
        if (fileExtension is ".exe")
        {
            Log.Information("Loading icon from exe..");
            return GetIconFromExe(path);
        }
        
        using var bitmap = new Bitmap(path);
        if (bitmap is { Height: 0, Width: 0 })
            return Global.DefaultIcon;
        
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;
        
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        
        return new Image { Source = bitmapImage };
    }

    private static Image GetIconFromExe(string path)
    {
        // Never call method directly - use GetIconFromFile instead
        
        var bitmap = Icon.ExtractAssociatedIcon(path)?.ToBitmap();
        if (bitmap is null)
        {
            Log.Error("Failed to get icon from file - GetIconFromExe");
            return Global.DefaultIcon;
        }

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();

        return new Image { Source = bitmapImage };
    }
}