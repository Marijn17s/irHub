using System;
using System.IO;
using System.Linq;
using System.Windows;
using irHub.Classes;
using irHub.Windows;
using Serilog;
using Velopack;

namespace irHub;

public partial class App
{
    private static string? _signalFilePath;
    private FileSystemWatcher? _fileWatcher;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        VelopackApp.Build().Run();
        
        Global.StartMinimizedArgument = e.Args.Contains("--minimized");
        
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var irHubDirectory = Path.Combine(documents, "irHub");
        _signalFilePath = Path.Combine(irHubDirectory, "irHub.signal");
        
        if (Global.FindProcess() is { } alreadyRunningProcess)
        {
            Log.Debug($"Another instance is already running with process id: {alreadyRunningProcess.Id}");
            NotifyExistingInstance();
            Current.Shutdown();
            return;
        }
        
        StartAsPrimaryInstance();
    }
    
    private void StartAsPrimaryInstance()
    {
        Log.Debug("Starting as primary instance..");
        
        if (File.Exists(_signalFilePath))
            File.Delete(_signalFilePath);
        
        _fileWatcher = new FileSystemWatcher
        {
            Path = Path.GetDirectoryName(_signalFilePath) ?? Directory.GetCurrentDirectory(),
            Filter = Path.GetFileName(_signalFilePath) ?? "",
            NotifyFilter = NotifyFilters.FileName
        };

        _fileWatcher.Created += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (MainWindow is not MainWindow mainWindow) return;
                
                mainWindow.RecoverFromTray();
                if (File.Exists(_signalFilePath))
                    File.Delete(_signalFilePath);
            });
        };

        _fileWatcher.EnableRaisingEvents = true;
    }

    private static void NotifyExistingInstance()
    {
        Log.Debug("Notifying existing instance..");
        
        if (string.IsNullOrEmpty(_signalFilePath)) return;
        File.WriteAllText(_signalFilePath, string.Empty);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        
        Log.Debug("Closing application..");
        Log.CloseAndFlush();
        
        _fileWatcher?.Dispose();
        if (File.Exists(_signalFilePath))
            File.Delete(_signalFilePath);
    }
}