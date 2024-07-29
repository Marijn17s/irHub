using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using HandyControl.Controls;
using HandyControl.Data;
using irHub.Classes;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Dialogs;
using MessageBox = HandyControl.Controls.MessageBox;

namespace irHub.Windows
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddProgram_OnClick(object sender, RoutedEventArgs e)
        {
            var program = new Program();
            var dialog = Dialog.Show(new ProgramDialog(ref program));
        }
        
        private async void StartAll_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var program in Global.Programs)
                await Global.StartProgram(program);
        }
        
        private void StopAll_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var program in Global.Programs)
                Global.StopProgram(program);
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

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
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
                    Global.StopProgram(program);
                Process.GetCurrentProcess().Kill();
            }
            if (dialog == MessageBoxResult.No)
                Process.GetCurrentProcess().Kill();
            if (dialog is MessageBoxResult.Cancel)
                e.Cancel = true;
        }
    }
}