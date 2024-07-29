using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Dialogs;
using iRacingSdkWrapper;
using MaterialDesignThemes.Wpf;
using Card = HandyControl.Controls.Card;

namespace irHub.Pages;

public partial class ProgramListPage
{
    public ProgramListPage()
    {
        InitializeComponent();
        LoadPrograms();
        CheckProgramsRunning();
        
        var client = new SdkWrapper();
        client.Start();
        client.Connected += async (_, _) =>
        {
            foreach (var program in Global.Programs.Where(program => program is { StartWithIracingSim: true, State: ProgramState.Stopped }))
                await Global.StartProgram(program);
        };

        client.Disconnected += (_, _) =>
        {
            foreach (var program in Global.Programs.Where(program => program is { StopWithIracingSim: true, State: ProgramState.Running }))
                Global.StopProgram(program);
        };
        
        // todo constantly loop or find other way to hook into iRacingUI.exe for start and stop
        /*if (Process.GetProcesses().Any(process => process.ProcessName.Contains("iRacingUI", StringComparison.InvariantCultureIgnoreCase)))
            foreach (var program in Global.Programs.Where(program => program is { StartWithIracingUI: true, State: ProgramState.Stopped }))
                Global.StartProgram(program);*/
    }

    private void LoadPrograms()
    {
        foreach (var program in Global.Programs)
        {
            CreateCard(program);
        }
        // todo Load in from json file or something (handle changes in config by giving default values if no valid value is found)
    }

    private static void CheckProgramsRunning()
    {
        foreach (var program in Global.Programs)
            Global.IsProgramRunning(program);
    }
    
    private void CreateCard(Program program)
    {
        var card = new Card
        {
            Background = Brushes.LightGray,
            BorderThickness = new Thickness(0),
            Effect = (Effect)FindResource("EffectShadow2"), // Assuming you have this effect defined in your resources
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

        var image = new Image
        {
            Height = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(5, 5, 0, 5),
            Source = new BitmapImage(new Uri("pack://application:,,,/irHub;component/Resources/logo.png")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(image, 0);
        headerGrid.Children.Add(image);

        var textBlock = new TextBlock
        {
            FontSize = 16,
            Foreground = Brushes.Black,
            FontWeight = FontWeights.SemiBold,
            Text = program.Name,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(textBlock, 1);
        headerGrid.Children.Add(textBlock);

        Grid.SetRow(headerGrid, 0);
        mainGrid.Children.Add(headerGrid);
        
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var actionButton = new Button
        {
            Foreground = Brushes.Black,
            Background = Brushes.LightGray,
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
            Background = Brushes.LightGray,
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
            Foreground = Brushes.Black,
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
            var success = await Global.StartProgram(program);
            
            if (success)
                return;
        }

        if (program.Process is null || program.Process.HasExited)
        {
            program.ChangeState(ProgramState.Stopped);
            Global.KillProcessesByPartialName(program.ExecutableName);
            return;
        }
        
        if (program.State is ProgramState.Running)
            Global.StopProgram(program);
    }
    
    private void EditButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Tag is not Program program) return;
        
        Dialog.Show(new ProgramDialog(ref program));
    }
    
    private void ActionButton_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.Tag is not Program program) return;
        
        if (program.State is ProgramState.Stopped)
        {
            button.Background = Brushes.Silver;
        }
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
        {
            button.Background = Brushes.LightGray;
        }
        if (program.State is ProgramState.Running)
        {
            button.Background = Brushes.LightGreen;
            button.Content = "RUNNING";
        }
    }
    
    private void EditButton_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        button.Background = Brushes.Silver;
    }
    
    private void EditButton_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        button.Background = Brushes.LightGray;
    }
    #endregion
}