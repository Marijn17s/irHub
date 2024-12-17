using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Dialogs;
using MaterialDesignThemes.Wpf;
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
            Global.ShouldShowGarageCover = isGarageMenuOpen.Value;
        };
        
        Global.iRacingClient.Connected += async (_, _) =>
        {
            _ = Task.Run(async () => await ServeGarageCover());
            
            foreach (var program in Global.Programs.Where(program => program is { StartWithIracingSim: true, State: ProgramState.Stopped }))
                await Global.StartProgram(program);
        };

        Global.iRacingClient.Disconnected += async (_, _) =>
        {
            foreach (var program in Global.Programs.Where(program => program is { StopWithIracingSim: true, State: ProgramState.Running }))
                await Global.StopProgram(program);
        };
    }

    private async void OnLoaded(object o, RoutedEventArgs routedEventArgs)
    {
        while (!Global.CancelIracingUiStateCheck)
        {
            try
            {
                if (Global.NeedsProgramRefresh)
                {
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
            catch { Global.CancelIracingUiStateCheck = true; }
        }
    }

    private void LoadPrograms()
    {
        ProgramsCardPanel.Children.Clear();
        foreach (var program in Global.Programs)
            CreateCard(program);
    }

    private static async Task CheckProgramsRunning()
    {
        foreach (var program in Global.Programs)
        {
            var exists = File.Exists(program.FilePath);
            if (!exists)
            {
                await program.ChangeState(ProgramState.NotFound);
                continue;
            } 
            Global.IsProgramRunning(program);
        }
    }

    private async Task ServeGarageCover()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8081/");
        listener.Start();
        Console.WriteLine("Server started. Serving on http://localhost:8081/");
        
        while (Global.iRacingClient.IsConnected)
        {
            var context = await listener.GetContextAsync();
            var response = context.Response;
            var requestUrl = context.Request.RawUrl;
            
            if (requestUrl is "/irhub")
            {
                // Serve the HTML file
                response.ContentType = "text/html";
                var html = await File.ReadAllTextAsync("C:\\Users\\marij\\Documents\\irHub\\garagecover.html");
                await using var writer = new StreamWriter(response.OutputStream);
                await writer.WriteAsync(html);
            }
            else if (requestUrl is "/state")
            {
                // Serve the current visibility state as JSON
                response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new { Global.ShouldShowGarageCover });
                await using var writer = new StreamWriter(response.OutputStream);
                await writer.WriteAsync(json);
            }
            else if (requestUrl is "/image")
            {
                var imagePath = "C:\\Users\\marij\\Downloads\\garagecover.jpg";
                if (File.Exists(imagePath))
                {
                    byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
                    response.ContentType = "image/jpeg";
                    response.ContentLength64 = imageBytes.Length;
                    await response.OutputStream.WriteAsync(imageBytes);
                }
            }
            response.Close();
        }
    }
    
    private void CreateCard(Program program)
    {
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
        actionButton.Click += ActionButton_OnClick;
        actionButton.MouseEnter += ActionButton_OnMouseEnter;
        actionButton.MouseLeave += ActionButton_OnMouseLeave;
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
        editButton.Click += EditButton_OnClick;
        editButton.MouseEnter += EditButton_OnMouseEnter;
        editButton.MouseLeave += EditButton_OnMouseLeave;
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

    #region Events
    private static async void ActionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Tag is not Program program) return;
        
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
        catch (InvalidOperationException)
        {
            await program.ChangeState(ProgramState.Stopped);
            Global.KillProcessesByPartialName(program.ExecutableName);
            return;
        }
        
        if (program.State is ProgramState.Running)
            await Global.StopProgram(program);
    }
    
    private void EditButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Tag is not Program program) return;

        // Blur window to avoid distractions and to seperate the windows
        if (Application.Current.MainWindow is Window mainWindow)
            mainWindow.Effect = Global.WindowBlurEffect;
        
        var t = new ProgramDialog(ref program);
        t.ShowDialog();

        // Disable window blur
        if (Application.Current.MainWindow is Window mainWindow2)
            mainWindow2.Effect = null;
        
        //CollectionViewSource.GetDefaultView(Global.Programs).Refresh();
    }
    
    private void ActionButton_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Tag is not Program program) return;
        
        if (program.State is ProgramState.Stopped)
            button.Background = new SolidColorBrush(Color.FromRgb(81, 91, 99));
        if (program.State is ProgramState.Running)
        {
            button.Background = Brushes.IndianRed;
            button.Content = "STOP";
        }
    }
    
    private void ActionButton_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Tag is not Program program) return;

        if (program.State is ProgramState.Stopped)
            button.Background = new SolidColorBrush(Color.FromRgb(55, 58, 62));
        if (program.State is ProgramState.Running)
        {
            button.Background = Brushes.LightGreen;
            button.Content = "RUNNING";
        }
    }
    
    private void EditButton_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        button.Background = new SolidColorBrush(Color.FromRgb(81, 91, 99));
    }
    
    private void EditButton_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        button.Background = new SolidColorBrush(Color.FromRgb(55, 58, 62));
    }
    #endregion
}