using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private static bool _ownsMutex;
    private static readonly string MutexName = "Global\\irHub_SingleInstance_Mutex_" + Environment.UserName;
    
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        VelopackApp.Build().Run();
        
        Global.StartMinimizedArgument = e.Args.Contains("--minimized");
        
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var irHubDirectory = Path.Combine(documents, "irHub");
        _signalFilePath = Path.Combine(irHubDirectory, "irHub.signal");
        
        try
        {
            _applicationMutex = new Mutex(true, MutexName, out _ownsMutex);
            Log.Debug($"Mutex '{MutexName}' created. Owns mutex: {_ownsMutex}");
        }
        catch (AbandonedMutexException)
        {
            Log.Warning($"Mutex '{MutexName}' was abandoned. This instance is taking ownership as the primary instance.");
            _ownsMutex = true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to create or acquire mutex '{MutexName}': {ex.Message}");
            _ownsMutex = false;
        }
        
        if (!_ownsMutex)
        {
            Log.Information("Another instance is already running. Notifying the existing instance and shutting down this one.");
            NotifyExistingInstance();
            _applicationMutex?.Dispose();
            _applicationMutex = null;
            Current.Shutdown();
            return;
        }
        
        Log.Information("This is the primary instance. Continuing startup.");
        await StartAsPrimaryInstance();
    }
    
    private async Task StartAsPrimaryInstance()
    {
        Log.Debug("Starting as primary instance..");

        while (!Directory.Exists(Global.irHubDirectoryPath))
            await Task.Delay(100);
        
        if (File.Exists(_signalFilePath))
        {
            Log.Debug("Signal file from previous session found. Deleting it.");
            File.Delete(_signalFilePath);
        }
        
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
                
                Log.Information("Signal file detected. Restoring main window from tray.");
                mainWindow.RecoverFromTray();
                if (File.Exists(_signalFilePath))
                {
                    Log.Debug("Deleting signal file after restoring window.");
                    File.Delete(_signalFilePath);
                }
            });
        };

        _fileWatcher.EnableRaisingEvents = true;
        Log.Debug("FileSystemWatcher for signal file enabled.");
    }

    private static void NotifyExistingInstance()
    {
        Log.Information("Notifying the already running instance to restore its window.");
        
        if (string.IsNullOrEmpty(_signalFilePath))
        {
            Log.Warning("Signal file path is null or empty. Cannot notify existing instance.");
            return;
        }
        
        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(_signalFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Log.Debug($"Creating directory for signal file: {directory}");
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_signalFilePath, string.Empty);
            Log.Debug("Signal file written to notify existing instance.");
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to notify existing instance by writing signal file: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        
        Log.Debug("Closing application..");
        
        try
        {
            if (_applicationMutex is not null && _ownsMutex)
            {
                _applicationMutex.ReleaseMutex();
                _applicationMutex.Dispose();
                _applicationMutex = null;
                Log.Debug($"Released and disposed mutex '{MutexName}'.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Error releasing or disposing mutex '{MutexName}' on exit: {ex.Message}");
        }
        
        _fileWatcher?.Dispose();
        if (File.Exists(_signalFilePath))
        {
            try
            {
                File.Delete(_signalFilePath);
                Log.Debug("Signal file deleted on application exit.");
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to delete signal file on exit: {ex.Message}");
            }
        }
    }
}