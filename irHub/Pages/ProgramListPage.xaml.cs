using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Dialogs;
using MaterialDesignThemes.Wpf;
using Serilog;
using Card = HandyControl.Controls.Card;
using Window = HandyControl.Controls.Window;

namespace irHub.Pages;

public partial class ProgramListPage
{
    public ProgramListPage()
    {
        InitializeComponent();
        LoadPrograms();
        Task.Run(async () =>
        {
            await CheckProgramsRunning();
        });

        Loaded += OnLoaded;
        
        Global.iRacingClient.Start();
        Global.iRacingClient.ConnectSleepTime = 200;

        Global.iRacingClient.TelemetryUpdated += (_, args) =>
        {
            var isGarageMenuOpen = args.TelemetryInfo.IsInGarage;
        };
        
        Global.iRacingClient.Connected += async (_, _) =>
        {
            Log.Information("Connected to iRacing SDK");
            
            foreach (var program in Global.Programs.Where(program => program is { StartWithIracingSim: true, State: ProgramState.Stopped }))
                await Global.StartProgram(program);
        };

        Global.iRacingClient.Disconnected += async (_, _) =>
        {
            Log.Information("Disconnected from iRacing SDK");
            
            foreach (var program in Global.Programs.Where(program => program is { StopWithIracingSim: true, State: ProgramState.Running }))
                await Global.StopProgram(program);
        };
    }

    private async void OnLoaded(object o, RoutedEventArgs routedEventArgs)
    {
        Log.Information("Program list page loaded");
        
        while (!Global.CancelIracingUiStateCheck)
        {
            try
            {
                if (Global.NeedsProgramRefresh)
                {
                    Log.Information("Programs need a refresh. Refreshing now..");
                    LoadPrograms();
                    Global.NeedsProgramRefresh = false;
                }

                if (Process.GetProcesses().Any(process =>
                        process.ProcessName.Contains("iRacingUI", StringComparison.InvariantCultureIgnoreCase)))
                    foreach (var program in Global.Programs.Where(program =>
                                 program is { StartWithIracingUi: true, State: ProgramState.Stopped }))
                        await Global.StartProgram(program);
                else
                {
                    foreach (var program in Global.Programs.Where(program =>
                                 program is { StopWithIracingUi: true, State: ProgramState.Running }))
                        await Global.StopProgram(program);
                }

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Log.Error($"Program list page threw the following error: {ex.Message} {ex.StackTrace} {ex.InnerException} {ex.Source}");
                Global.CancelIracingUiStateCheck = true;
            }
        }
    }

    private void LoadPrograms()
    {
        Log.Information("Creating cards for programs");
        
        ProgramsCardPanel.Children.Clear();
        foreach (var program in Global.Programs)
            CreateCard(program);
    }

    private static async Task CheckProgramsRunning()
    {
        Log.Information("Checking if programs still exist and are running..");
        foreach (var program in Global.Programs)
        {
            var exists = File.Exists(program.FilePath);
            if (!exists)
            {
                Log.Warning($"Program {program.FilePath} doesn't exist (anymore)");
                await program.ChangeState(ProgramState.NotFound);
                continue;
            } 
            Global.IsProgramRunning(program);
        }
    }
    
    private void CreateCard(Program program)
    {
        Log.Information($"Creating program card for {program.Name}..");
        
        var card = new Card
        {
            Background = new SolidColorBrush(Color.FromRgb(55, 58, 62)),
            BorderThickness = new Thickness(0),
            Effect = (Effect)FindResource("EffectShadow2"),
            Height = 110,
            Margin = new Thickness(5),
            Width = 250
        };

        // Create the main grid with two rows
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });

        // Create the content for the first row (header)
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBinding = new Binding("Source") { Source = program, Path = new PropertyPath("Icon.Source") };
        var image = new Image
        {
            Height = 60,
            Width = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(5, 5, 0, 5),
            VerticalAlignment = VerticalAlignment.Center
        };
        image.SetBinding(Image.SourceProperty, iconBinding);
        
        Grid.SetColumn(image, 0);
        headerGrid.Children.Add(image);

        var nameBinding = new Binding("Text") { Source = program, Path = new PropertyPath("Name") };
        var textBlock = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBlock.SetBinding(TextBlock.TextProperty, nameBinding);
        
        Grid.SetColumn(textBlock, 1);
        headerGrid.Children.Add(textBlock);

        Grid.SetRow(headerGrid, 0);
        mainGrid.Children.Add(headerGrid);
        
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var actionButton = new Button
        {
            Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            Background = new SolidColorBrush(Color.FromRgb(55, 58, 62)),
            BorderThickness = new Thickness(0),
            Content = "START",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Height = 35,
            Width = 200,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = program
        };
        program.ActionButton = actionButton;
        BorderElement.SetCornerRadius(actionButton, new CornerRadius(8));
        actionButton.Click += async (_, _) =>
        {
            if (program.State is ProgramState.Stopped)
            {
                await Global.StartProgram(program);
                return;
            }

            if (program.State is ProgramState.NotFound)
            {
                var exists = File.Exists(program.FilePath);
                if (exists)
                {
                    await program.ChangeState(ProgramState.Stopped);
                    await Global.StartProgram(program);
                }
                return;
            }

            try
            {
                if (program.Process is null || program.Process.HasExited)
                {
                    await program.ChangeState(ProgramState.Stopped);
                    Global.KillProcessesByPartialName(program.ExecutableName);
                    return;
                }
            }
            catch (InvalidOperationException ex)
            {
                Log.Error($"Program list page threw the following error: {ex.Message} {ex.StackTrace} {ex.InnerException} {ex.Source}");
            
                await program.ChangeState(ProgramState.Stopped);
                Global.KillProcessesByPartialName(program.ExecutableName);
                return;
            }
        
            if (program.State is ProgramState.Running)
                await Global.StopProgram(program);
        };
        actionButton.MouseEnter += (_, _) =>
        {
            if (program.State is ProgramState.Stopped)
                actionButton.Background = new SolidColorBrush(Color.FromRgb(81, 91, 99));
            if (program.State is ProgramState.Running)
            {
                actionButton.Background = Brushes.IndianRed;
                actionButton.Content = "STOP";
            }
        };
        actionButton.MouseLeave += (_, _) =>
        {
            if (program.State is ProgramState.Stopped)
                actionButton.Background = new SolidColorBrush(Color.FromRgb(55, 58, 62));
            if (program.State is ProgramState.Running)
            {
                actionButton.Background = Brushes.LightGreen;
                actionButton.Content = "RUNNING";
            }
        };
        Grid.SetColumn(actionButton, 0);
        footerGrid.Children.Add(actionButton);

        var editButton = new Button
        {
            Background = new SolidColorBrush(Color.FromRgb(55, 58, 62)),
            BorderThickness = new Thickness(0),
            Height = 35,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 40,
            Tag = program
        };
        BorderElement.SetCornerRadius(editButton, new CornerRadius(8));
        editButton.Click += (_, _) =>
        {
            // Blur window to avoid distractions and to seperate the windows
            if (Application.Current.MainWindow is Window mainWindow)
                mainWindow.Effect = Global.WindowBlurEffect;

            var t = new ProgramDialog(ref program);
            t.ShowDialog();

            // Disable window blur
            if (Application.Current.MainWindow is Window mainWindow2)
                mainWindow2.Effect = null;
        };
        editButton.MouseEnter += (_, _) => editButton.Background = new SolidColorBrush(Color.FromRgb(81, 91, 99));
        editButton.MouseLeave += (_, _) => editButton.Background = new SolidColorBrush(Color.FromRgb(55, 58, 62));
        Grid.SetColumn(editButton, 1);

        var pencilIcon = new PackIcon
        {
            Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            Height = 25,
            Kind = PackIconKind.PencilOutline,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 40
        };
        editButton.Content = pencilIcon;
        footerGrid.Children.Add(editButton);

        Grid.SetRow(footerGrid, 1);
        mainGrid.Children.Add(footerGrid);

        card.Content = mainGrid;

        ProgramsCardPanel.Children.Add(card);
    }
}