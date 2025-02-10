using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Windows;
using Microsoft.Win32;
using Serilog;
using MessageBox = HandyControl.Controls.MessageBox;
using Path = System.IO.Path;

namespace irHub.Dialogs;

public partial class ProgramDialog : INotifyPropertyChanged
{
    private readonly bool _isNew;
    
    private Program? _program;
    public Program? Program
    {
        get => _program;
        set
        {
            _program = value;
            OnPropertyChanged();
        }
    }
    
    private Program? _originalProgram;
    public Program? OriginalProgram
    {
        get => _originalProgram;
        set
        {
            _originalProgram = value;
            OnPropertyChanged();
        }
    }
    
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
    
    internal ProgramDialog(ref Program program, bool isNew = false)
    {
        InitializeComponent();
        
        Log.Information($"Opening program dialog. New: {isNew}");
        if (Application.Current.MainWindow is MainWindow mainWindow)
            Owner = mainWindow;
        
        Program = program;
        DataContext = Program;
        
        if (!program.UseExecutableIcon) 
            ResetIconButton.Visibility = Visibility.Visible;
        
        _isNew = isNew;
        if (_isNew)
        {
            var executableInfo = new FileInfo(program.FilePath);
            var executableNameWithExtension = executableInfo.Name;
            var lastDot = executableNameWithExtension.LastIndexOf('.');
            var executableName = executableNameWithExtension[..lastDot];
            Program.Name = executableName;
            return;
        }

        OriginalProgram = Global.DeepCloneT(program);
        // Manually copy over properties that can't be parsed to JSON (1/2)
        // todo make it possible to convert these properties to JSON (maybe base64 encoding it?) 
        if (OriginalProgram is null)
        {
            Log.Warning("Original program could not be loaded.");
            return;
        }
        
        OriginalProgram.Icon = program.Icon;
        OriginalProgram.Process = program.Process;
        OriginalProgram.ActionButton = program.ActionButton;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Save changes
        Log.Information($"Saving program.. New: {_isNew} - ProgramDialog");
        
        if (Program?.Name is "" || Program?.Name?.Length > 20)
        {
            MessageBox.Warning("The program must have a name with a maximum length of 20 characters.");
            return;
        }
        
        if (_isNew && Program is not null)
            Global.Programs.Add(Program);
        
        Global.SavePrograms();
        
        // Seperated from other check because it needs to be in this order
        if (_isNew)
            Global.RefreshPrograms();
        
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Focus();
        
        Close();
        
        Log.Information($"Successfully saved program {Program?.Name}");
        Growl.Success("Successfully saved program!");
    }
    
    private async void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Cancel changes
        Log.Information($"Canceling program dialog. New: {_isNew} - ProgramDialog");
        
        if (_isNew)
        {
            // todo handle new
        }
        
        if (!_isNew && OriginalProgram is not null && Program is not null)
        {
            Global.CopyProperties(OriginalProgram, Program);
            // Manually copy over properties that can't be parsed to JSON (2/2)
            Program.Icon = OriginalProgram.Icon;
            Program.Process = OriginalProgram.Process;
            Program.ActionButton = OriginalProgram.ActionButton;
            if (Program.State is not ProgramState.NotFound && !File.Exists(Program.FilePath))
                await Program.ChangeState(ProgramState.NotFound);
        }
        
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Focus();
        Close();
    }
    
    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Delete program
        Log.Information($"Deleting {Program?.Name}..");
        
        if (!_isNew && OriginalProgram is not null && Program is not null)
            Global.Programs.Remove(Program);
        
        var name = Program?.Name ?? OriginalProgram?.Name;
        Program = null;
        OriginalProgram = null;
        Global.SavePrograms();
        Global.RefreshPrograms();
        
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Focus();
        Close();
        
        Log.Information($"Successfully deleted program {name}");
        Growl.Success("Successfully removed program!");
    }

    private void ShowNewIconDialog()
    {
        Log.Information("Opening new icon dialog");
        
        Program ??= new Program();
        
        var dialog = new OpenFileDialog
        {
            Filter = "Images / Icons (*.exe, *.jpg, *.jpeg, *.png, *.ico)|*.exe;*.jpg;*.jpeg;*.png;*.ico",
            InitialDirectory = Path.GetDirectoryName(Program.FilePath),
            Multiselect = false,
            Title = "Please select an image. A resolution of at least 100x100 and 1:1 aspect ratio is recommended."
        };

        if (dialog.ShowDialog() is not true || dialog.FileName is "")
        {
            Log.Warning("No new icon was selected");
            ResetToExecutableIcon();
            return;
        }
        if (dialog.FileName == Program.FilePath)
        {
            ResetToExecutableIcon();
            return;
        }
        
        Program.SetIcon(dialog.FileName);
        ResetIconButton.Visibility = Visibility.Visible;
    }
    
    private void ShowNewApplicationDialog()
    {
        Log.Information("Opening new executable dialog");
        
        Program ??= new Program();
        
        var dialog = new OpenFileDialog
        {
            Filter = "Executables (*.exe, *.bat, *.cmd)|*.exe;*.bat;*.cmd",
            InitialDirectory = Path.GetDirectoryName(Program.FilePath),
            Multiselect = false,
            Title = "Please select an application you would like to add"
        };

        if (dialog.ShowDialog() is not true || dialog.FileName is "")
        {
            Log.Warning("No new executable was selected");
            return;
        }
        
        Program.FilePath = dialog.FileName;
        if (!Program.UseExecutableIcon) return;
        
        ResetToExecutableIcon();
    }

    private void ResetToExecutableIcon()
    {
        Log.Information($"Resetting icon of {Program?.Name} to executable icon..");
        
        Program ??= new Program();
        Program.SetExecutableIcon();
        ResetIconButton.Visibility = Visibility.Hidden;
    }

    private void SelectNewIcon_OnClick(object sender, RoutedEventArgs e) => ShowNewIconDialog();
    
    private void SelectNewApplication_OnClick(object sender, RoutedEventArgs e) => ShowNewApplicationDialog();

    private void ResetIconButton_OnClick(object sender, RoutedEventArgs e) => ResetToExecutableIcon();

    private async void ProgramDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        Log.Information("Initializing program difference checking..");
        while (Program is not null)
        {
            var changedProgram = JsonSerializer.Serialize(Program);
            var originalProgram = JsonSerializer.Serialize(OriginalProgram);
            SaveButton.Visibility = changedProgram != originalProgram ? Visibility.Visible : Visibility.Hidden;
            await Task.Delay(50);
        }
    }
}