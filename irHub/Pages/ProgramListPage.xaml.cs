using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
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
        Unloaded += OnUnloaded;
        
        Global.iRacingClient.Start();
        Global.iRacingClient.UpdateInterval = 60;
        
        Global.iRacingClient.OnConnected += async () =>
        {
            Log.Information("iRacing SDK: Connected");
            
            foreach (var program in Global.Programs.Where(program => program is { StartWithIracingSim: true, State: ProgramState.Stopped }))
                await Global.StartProgram(program);
        };
        
        Global.iRacingClient.OnDisconnected += async () =>
        {
            Log.Information("iRacing SDK: Disconnected");

            foreach (var program in Global.Programs.Where(program => program is { StopWithIracingSim: true, State: ProgramState.Running }))
                await Global.StopProgram(program);
        };
    }

    private void OnLoaded(object o, RoutedEventArgs routedEventArgs)
    {
        Log.Information("Program list page loaded");
        
        Global.IracingUiStateCheckCancellationTokenSource ??= new CancellationTokenSource();
        _ = Task.Run(async () => await MonitorIracingProcessesAsync(Global.IracingUiStateCheckCancellationTokenSource.Token));
    }
    
    private async Task MonitorIracingProcessesAsync(CancellationToken cancellationToken)
    {
        const int maxErrorCount = 5;
        const int refreshIntervalMs = 5000;
        const int processCheckIntervalMs = 1000;
        
        var errorCount = 0;
        var lastUiState = false;
        var lastSimState = false;
        var lastRefreshTime = DateTime.MinValue;

        Log.Information("Starting iRacing process monitoring loop");

        while (!cancellationToken.IsCancellationRequested && !Global.CancelIracingUiStateCheck)
        {
            try
            {
                var currentTime = DateTime.Now;
                
                if (Global.NeedsProgramRefresh && (currentTime - lastRefreshTime).TotalMilliseconds >= refreshIntervalMs)
                {
                    Log.Information("Programs need a refresh. Refreshing now..");
                    
                    await Dispatcher.InvokeAsync(LoadPrograms, DispatcherPriority.Normal, cancellationToken);
                    
                    Global.NeedsProgramRefresh = false;
                    lastRefreshTime = currentTime;
                }

                var uiProcesses = Process.GetProcessesByName("iRacingUI");
                var isUiOpen = uiProcesses.Length > 0;
                
                if (isUiOpen != lastUiState)
                {
                    lastUiState = isUiOpen;
                    
                    if (isUiOpen)
                    {
                        Log.Information("iRacingUI was just opened");
                        var programsToStart = Global.Programs
                            .Where(program => program is { StartWithIracingUi: true, State: ProgramState.Stopped })
                            .ToList();
                        
                        foreach (var program in programsToStart)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            await Global.StartProgram(program).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        Log.Information("iRacingUI was just closed");
                        var programsToStop = Global.Programs
                            .Where(program => program is { StopWithIracingUi: true, State: ProgramState.Running })
                            .ToList();
                        
                        foreach (var program in programsToStop)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            await Global.StopProgram(program).ConfigureAwait(false);
                        }
                    }
                }
                
                var simProcesses = Process.GetProcessesByName("iRacingSim64DX11");
                var isSimOpen = simProcesses.Length > 0;
                
                if (isSimOpen != lastSimState)
                {
                    lastSimState = isSimOpen;

                    Log.Information(isSimOpen ? "iRacing Sim was just opened" : "iRacing Sim was just closed");
                }

                // Clean up process objects to prevent memory leaks
                var uiProcessSpan = uiProcesses.AsSpan();
                foreach (var process in uiProcessSpan)
                    process.Dispose();
                var simProcessSpan = simProcesses.AsSpan();
                foreach (var process in simProcessSpan)
                    process.Dispose();
                
                errorCount = 0;
                
                await Task.Delay(processCheckIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("iRacing process monitoring was cancelled");
                break;
            }
            catch (Exception ex)
            {
                errorCount++;
                Log.Error($"Program list page threw the following error (attempt {errorCount}/{maxErrorCount}): {ex.Message} {ex.StackTrace} {ex.InnerException} {ex.Source}");
                
                if (errorCount >= maxErrorCount)
                {
                    Log.Error("Maximum error count reached, stopping iRacing UI monitoring");
                    Global.CancelIracingUiStateCheck = true;
                    break;
                }
                
                await Task.Delay(processCheckIntervalMs, cancellationToken);
            }
        }
        
        Log.Information("iRacing process monitoring loop ended");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Log.Information("Program list page unloaded, cancelling monitoring tasks");
        Global.CleanupResources();
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
        
        var programsToCheck = Global.Programs.ToList(); // Create a snapshot to avoid collection modification issues
        foreach (var program in programsToCheck)
        {
            var exists = File.Exists(program.FilePath);
            if (!exists)
            {
                Log.Warning($"Program {program.FilePath} doesn't exist (anymore)");
                await program.ChangeState(ProgramState.NotFound).ConfigureAwait(false);
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