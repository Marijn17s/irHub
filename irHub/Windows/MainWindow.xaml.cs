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
using Microsoft.Win32;
using MessageBox = HandyControl.Controls.MessageBox;

namespace irHub.Windows
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            InitialChecks();
            
            // Open on center of screen
            Left = (SystemParameters.WorkArea.Width - Width) / 2;
            Top = (SystemParameters.WorkArea.Height - Height) / 2;
        }

        private void InitialChecks()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Global.irHubDirectoryPath = Path.Combine(documents, "irHub");
            if (!Path.Exists(Global.irHubDirectoryPath))
                Directory.CreateDirectory(Global.irHubDirectoryPath);

            if (!File.Exists(Path.Combine(Global.irHubDirectoryPath, "programs.json")))
                File.WriteAllText(Path.Combine(Global.irHubDirectoryPath, "programs.json"), "[]");
        }

        private static async Task CheckProgramStateLoop()
        {
            // todo optimize
            while (!Global.CancelStateCheck)
            {
                foreach (var program in Global.Programs)
                {
                    var running = Global.IsProgramRunning(program);
                    if (running && program.State == ProgramState.Stopped)
                        await program.ChangeState(ProgramState.Running);
                    if (!running && program.State == ProgramState.Running)
                        await program.ChangeState(ProgramState.Stopped);
                }
                await Task.Delay(500);
            }
        }

        private void AddProgram_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executables (*.exe, *.bat, *.cmd)|*.exe;*.bat;*.cmd",
                InitialDirectory = Environment.SpecialFolder.CommonProgramFiles.ToString(),
                Multiselect = false,
                Title = "Select an application you want to add"
            };

            if (dialog.ShowDialog() is not true || dialog.FileName is "")
                return;

            var program = new Program
            {
                FilePath = dialog.FileName,
                Icon = Global.GetIconFromFile(dialog.FileName)
            };
            var t = new ProgramDialog(ref program, true);
            t.ShowDialog();
        }
        
        private async void StartAll_OnClick(object sender, RoutedEventArgs e)
        {
            // todo check parallelization performance
            foreach (var program in Global.Programs)
                await Global.StartProgram(program);
            if (Global.Programs.Count > 0)
                Growl.Success("All programs were started successfully.");
        }
        
        private async void StopAll_OnClick(object sender, RoutedEventArgs e)
        {
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
                TrayIcon.Click += (_, _) =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    TrayIcon.Visibility = Visibility.Hidden;
                };
                Hide();
            }

            base.OnStateChanged(e);
        }

        private async void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            if (!Global.Programs.Any(program => program.State is ProgramState.Running)) Process.GetCurrentProcess().Kill();
            
            var info = new MessageBoxInfo
            {
                Button = MessageBoxButton.YesNoCancel,
                Caption = "Are you sure you want to close irHub?",
                Message = "Do you want to shut down all managed applications?",
                IconKey = ResourceToken.AskGeometry,
                IconBrushKey = ResourceToken.WarningBrush,
                    
            };
            
            var dialog = MessageBox.Show(info);
            if (dialog == MessageBoxResult.Yes)
            {
                foreach (var program in Global.Programs)
                    await Global.StopProgram(program);
                Process.GetCurrentProcess().Kill();
            }
            if (dialog == MessageBoxResult.No)
                Process.GetCurrentProcess().Kill();
            if (dialog is MessageBoxResult.Cancel)
                e.Cancel = true;
        }

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            await CheckProgramStateLoop();
        }
    }
}