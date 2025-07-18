﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using HandyControl.Controls;
using HandyControl.Data;
using irHub.Classes;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Dialogs;
using irHub.Helpers;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using Velopack;
using Velopack.Sources;
using MessageBox = HandyControl.Controls.MessageBox;

namespace irHub.Windows;

public partial class MainWindow
{
    private const int HotkeyId = 9000;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkI = 0x49; // 'I' key
    private const int WmHotkey = 0x0312;
    private bool _hotkeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
        InitialChecks();
        Global.LoadSettings();
            
        var logPath = Path.Combine(Global.irHubDirectoryPath, "logs");
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
            
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"{logPath}\\log-{timestamp}.txt", LogEventLevel.Verbose, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
            
        Log.Information("Application started");
        
        Global.Settings.PropertyChanged += Settings_PropertyChanged;
        
        // Open on center of screen
        Left = (SystemParameters.WorkArea.Width - Width) / 2;
        Top = (SystemParameters.WorkArea.Height - Height) / 2;
    }
        
    private async Task UpdateApplication()
    {
        var source = new GithubSource("https://github.com/Marijn17s/irHub", "", true, new HttpClientFileDownloader());
        var manager = new UpdateManager(source);
            
        // prevents error while debugging
        if (!manager.IsInstalled)
        {
            Log.Debug("Installation is not installed. Aborting update check");
            return;
        }

        Log.Debug("Checking for updates..");
        var currentVersion = manager.CurrentVersion;
        var newVersion = await manager.CheckForUpdatesAsync();
        if (newVersion is null)
        {
            Log.Debug("Latest application version is installed");
            return; // no update available
        }
            
        Log.Debug($"An update is available. Currently installed version: {currentVersion}. New version: {newVersion.TargetFullRelease.Version}");

        Effect = Global.WindowBlurEffect;
        var result = new UpdateDialog(currentVersion, newVersion).ShowDialog();
        Effect = null;
            
        if (result is not true) return;
            
        Log.Debug($"Downloading {newVersion} update..");
            
        await manager.DownloadUpdatesAsync(newVersion);
        manager.ApplyUpdatesAndRestart(newVersion);
    }

    private void InitialChecks()
    {
        Log.Information("Performing initial checks..");
        Log.Debug("Resolving application directory path..");
            
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Global.irHubDirectoryPath = Path.Combine(documents, "irHub");
        if (!Directory.Exists(Global.irHubDirectoryPath))
            Directory.CreateDirectory(Global.irHubDirectoryPath);
            
        Log.Debug("Resolved application directory path!");
            
        var logPath = Path.Combine(Global.irHubDirectoryPath, "logs");
        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);

        if (!File.Exists(Path.Combine(Global.irHubDirectoryPath, "programs.json")))
            File.WriteAllText(Path.Combine(Global.irHubDirectoryPath, "programs.json"), "[]");
            
        if (!File.Exists(Path.Combine(Global.irHubDirectoryPath, "settings.json")))
            File.WriteAllText(Path.Combine(Global.irHubDirectoryPath, "settings.json"), "{}");

        CleanupOldLogs(logPath);
    }

    private void CleanupOldLogs(string logPath)
    {
        // Cleans up log files older than 2 weeks to prevent disk space accumulation
        try
        {
            Log.Debug("Starting log cleanup process..");

            var logFiles = Directory.GetFiles(logPath, "*.txt");
            var cutoffDate = DateTime.Now.AddDays(-14); // 2 weeks ago
            var deletedCount = 0;

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(logFile);
                        deletedCount++;
                        Log.Debug($"Deleted old log file: {Path.GetFileName(logFile)}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed to delete log file {Path.GetFileName(logFile)}: {ex.Message}");
                    }
                }
            }
            Log.Information($"Cleaned up {deletedCount} old log file(s) older than 2 weeks");
        }
        catch (Exception ex)
        {
            Log.Error($"Error during log cleanup: {ex.Message}");
        }
    }

    private static async Task CheckProgramStateLoop()
    {
        Log.Information("Starting programs state check..");
        while (!Global.CancelStateCheck)
        {
            await Global.CheckProgramsRunning();
            await Task.Delay(2000);
        }
    }

    private void AddProgram_OnClick(object sender, RoutedEventArgs e)
    {
        Log.Information("Opening new program dialog");
            
        var dialog = new OpenFileDialog
        {
            Filter = "Executables (*.exe, *.bat, *.cmd)|*.exe;*.bat;*.cmd",
            InitialDirectory = Environment.SpecialFolder.CommonProgramFiles.ToString(),
            Multiselect = false,
            Title = "Select an application you want to add"
        };

        if (dialog.ShowDialog() is not true || dialog.FileName is "")
        {
            Log.Warning("No executable selected");
            return;
        }

        var program = new Program
        {
            FilePath = dialog.FileName,
            Icon = IconHelper.GetIconFromFile(dialog.FileName)
        };
        var t = new ProgramDialog(ref program, true);
        t.ShowDialog();
    }
        
    private async void StartAll_OnClick(object sender, RoutedEventArgs e)
    {
        Log.Information("Starting all programs");
        var (success, failed) = await Global.StartProgramsParallel(Global.Programs.Where(p => p.IncludeInStartAll));

        if (success + failed is 0) return;

        if (failed is not 0)
        {
            Growl.Error($"Failed to start {failed} programs.");
            return;
        }
        Growl.Success($"Successfully started {success} programs.");
    }
        
    private async void StopAll_OnClick(object sender, RoutedEventArgs e)
    {
        Log.Information("Stopping all programs");
        foreach (var program in Global.Programs)
        {
            if (!program.IncludeInStopAll) continue;
            await Global.StopProgram(program);
        }
        if (Global.Programs.Count > 0)
            Growl.Success("All programs were stopped successfully.");
    }
        
    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState is WindowState.Minimized)
        {
            TrayIcon.Visibility = Visibility.Visible;
            TrayIcon.Click += (_, _) => RecoverFromTray();
            Hide();
                
            Log.Information("Minimized to system tray");
        }

        base.OnStateChanged(e);
    }

    internal void RecoverFromTray()
    {
        Show();
        Focus();
        Activate();
        WindowState = WindowState.Normal;
        TrayIcon.Visibility = Visibility.Hidden;
            
        Log.Information("Recovered from system tray");
    }

    private async void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!Global.Programs.Any(program => program.State is ProgramState.Running && program.IncludeInStopAll))
        {
            UnregisterGlobalHotkey();
            Process.GetCurrentProcess().Kill();
        }
            
        var info = new MessageBoxInfo
        {
            Button = MessageBoxButton.YesNoCancel,
            Caption = "Are you sure you want to close irHub?",
            Message = "Do you want to shut down all managed applications?",
            IconKey = ResourceToken.AskGeometry,
            IconBrushKey = ResourceToken.WarningBrush,
        };
            
        var dialog = MessageBox.Show(info);
        if (dialog is MessageBoxResult.Yes)
        {
            foreach (var program in Global.Programs)
            {
                if (!program.IncludeInStopAll) continue;
                await Global.StopProgram(program);
            }

            UnregisterGlobalHotkey();
            Process.GetCurrentProcess().Kill();
        }
        if (dialog is MessageBoxResult.No)
        {
            UnregisterGlobalHotkey();
            Process.GetCurrentProcess().Kill();
        }
        if (dialog is MessageBoxResult.Cancel)
            e.Cancel = true;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await UpdateApplication();
        
        // Minimize if set in settings or if set in arguments
        if (Global.Settings.StartMinimized || Global.StartMinimizedArgument)
        {
            WindowState = WindowState.Minimized;
            Hide();
            Log.Information("Minimized to system tray");
        }
        
        // Register global hotkey if enabled
        if (Global.Settings.EnableGlobalHotkey)
            RegisterGlobalHotkey();
            
        Log.Information("MainWindow loaded");
        Global.MainWindowLoaded = true;
            
        await CheckProgramStateLoop();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(Global.Settings.EnableGlobalHotkey)) return;
        
        if (Global.Settings.EnableGlobalHotkey)
        {
            Log.Information("Global hotkey enabled - registering hotkey");
            RegisterGlobalHotkey();
        return;
        }

        Log.Information("Global hotkey disabled - unregistering hotkey");
        UnregisterGlobalHotkey();
    }

    private void RegisterGlobalHotkey()
    {
        if (_hotkeyRegistered) return;
        
        try
        {
            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndHook);
            
            var success = RegisterHotKey(helper.Handle, HotkeyId, ModControl | ModShift, VkI);
            if (success)
            {
                _hotkeyRegistered = true;
                Log.Information("Global hotkey registered successfully (Ctrl+Shift+I)");
            }
            else
                Log.Warning("Failed to register global hotkey - it may already be in use by another application");
        }
        catch (Exception ex)
        {
            Log.Error($"Error registering global hotkey: {ex.Message}");
        }
    }

    private void UnregisterGlobalHotkey()
    {
        if (!_hotkeyRegistered) return;
        
        try
        {
            var helper = new WindowInteropHelper(this);
            var success = UnregisterHotKey(helper.Handle, HotkeyId);
            if (success)
            {
                _hotkeyRegistered = false;
                Log.Information("Global hotkey unregistered successfully");
            }
            else
                Log.Warning("Failed to unregister global hotkey");
        }
        catch (Exception ex)
        {
            Log.Error($"Error unregistering global hotkey: {ex.Message}");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is not WmHotkey) return IntPtr.Zero;
        switch (wParam.ToInt32())
        {
            case HotkeyId:
                Log.Information("Global hotkey pressed - recovering from tray");
                RecoverFromTray();
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    #region DllImports
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    #endregion
}