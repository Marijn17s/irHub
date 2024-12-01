using System;
using System.IO;
using System.Windows;
using irHub.Classes;
using irHub.Windows;
using Velopack;

namespace irHub;

public partial class App
{
    private static string _signalFilePath;
    private FileSystemWatcher _fileWatcher;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        VelopackApp.Build().Run();
        
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var irHubDirectory = Path.Combine(documents, "irHub");
        _signalFilePath = Path.Combine(irHubDirectory, "irHub.signal");
        
        
        if (Global.FindProcess() is { } alreadyRunningProcess)
        {
            NotifyExistingInstance();
            Current.Shutdown();
            return;
        }
        
        StartAsPrimaryInstance();
    }
    
    private void StartAsPrimaryInstance()
    {
        if (File.Exists(_signalFilePath))
            File.Delete(_signalFilePath);
        
        _fileWatcher = new FileSystemWatcher
        {
            Path = Path.GetDirectoryName(_signalFilePath) ?? Directory.GetCurrentDirectory(),
            Filter = Path.GetFileName(_signalFilePath),
            NotifyFilter = NotifyFilters.FileName
        };

        _fileWatcher.Created += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (MainWindow is not MainWindow mainWindow) return;
                
                mainWindow.RecoverFromTray();
                File.Delete(_signalFilePath);
            });
        };

        _fileWatcher.EnableRaisingEvents = true;
    }

    private static void NotifyExistingInstance()
    {
        File.WriteAllText(_signalFilePath, string.Empty);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        
        _fileWatcher.Dispose();
        if (File.Exists(_signalFilePath))
            File.Delete(_signalFilePath);
    }
}