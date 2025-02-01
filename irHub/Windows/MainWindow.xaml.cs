using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

namespace irHub.Windows
{
    public partial class MainWindow
    {
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

            if (!File.Exists(Path.Combine(Global.irHubDirectoryPath, "garagecover.html")))
            {
                var html = EmbeddedResourceHelper.GetEmbeddedResource("irHub.Resources.garagecover.html");
                if (html is "")
                {
                    Global.Settings.ShowGarageCover = false;
                    Global.SaveSettings();
                    Growl.Error("Failed to construct garage cover! Garage cover is now disabled.");
                    return;
                }
                File.WriteAllText(Path.Combine(Global.irHubDirectoryPath, "garagecover.html"), html);
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
            // todo check parallelization performance
            Log.Information("Starting all programs");
            foreach (var program in Global.Programs)
                await Global.StartProgram(program);
            if (Global.Programs.Count > 0)
                Growl.Success("All programs were started successfully.");
        }
        
        private async void StopAll_OnClick(object sender, RoutedEventArgs e)
        {
            Log.Information("Stopping all programs");
            foreach (var program in Global.Programs)
                await Global.StopProgram(program);
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
            if (!Global.Programs.Any(program => program.State is ProgramState.Running))
                Process.GetCurrentProcess().Kill();
            
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
                    await Global.StopProgram(program);
                Process.GetCurrentProcess().Kill();
            }
            if (dialog is MessageBoxResult.No)
                Process.GetCurrentProcess().Kill();
            if (dialog is MessageBoxResult.Cancel)
                e.Cancel = true;
        }

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            await UpdateApplication();
            
            // Minimize if set in settings
            if (Global.Settings.StartMinimized)
            {
                WindowState = WindowState.Minimized;
                Hide();
                Log.Information("Minimized to system tray");
            }
            
            Log.Information("MainWindow loaded");
            Global.MainWindowLoaded = true;
            
            await CheckProgramStateLoop();
        }
    }
}