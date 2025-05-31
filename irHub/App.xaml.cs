using System;
using System.IO;
using System.Linq;
using System.Threading;
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
    private static Mutex? _applicationMutex;
    private static readonly string MutexName = "Global\\irHub_SingleInstance_Mutex_" + Environment.UserName;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        VelopackApp.Build().Run();
        
        Global.StartMinimizedArgument = e.Args.Contains("--minimized");
        
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var irHubDirectory = Path.Combine(documents, "irHub");
        _signalFilePath = Path.Combine(irHubDirectory, "irHub.signal");
        
        bool isNewInstance;
        try
        {
            _applicationMutex = new Mutex(true, MutexName, out isNewInstance);
        }
        catch (AbandonedMutexException)
        {
            Log.Warning("Detected abandoned mutex from previous instance - taking ownership");
            isNewInstance = true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to create application mutex: {ex.Message}");
            // Fallback: Clean up any orphaned processes and continue startup
            Log.Information("Attempting to clean up orphaned processes and continue startup");
            isNewInstance = true;
        }
        
        if (!isNewInstance)
        {
            Log.Debug("Another instance is already running (mutex check)");
            NotifyExistingInstance();
            
            // Release the mutex since we're not the primary instance
            _applicationMutex?.ReleaseMutex();
            _applicationMutex?.Dispose();
            _applicationMutex = null;
            
            Current.Shutdown();
            return;
        }
        
        Log.Debug("Successfully acquired application mutex - starting as primary instance");
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
        
        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(_signalFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_signalFilePath, string.Empty);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to notify existing instance: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        
        Log.Debug("Closing application..");
        
        try
        {
            if (_applicationMutex is not null)
            {
                _applicationMutex.ReleaseMutex();
                _applicationMutex.Dispose();
                _applicationMutex = null;
                Log.Debug("Released application mutex");
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Error releasing mutex on exit: {ex.Message}");
        }
        
        _fileWatcher?.Dispose();
        if (File.Exists(_signalFilePath))
            File.Delete(_signalFilePath);
    }
}