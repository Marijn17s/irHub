using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Models;
using irHub.Windows;
using Microsoft.Win32;
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
        if (Application.Current.MainWindow is MainWindow mainWindow)
            Owner = mainWindow;
        
        Program = program;
        DataContext = Program;
        
        if (!program.UseExecutableIcon) 
            ResetIconButton.Visibility = Visibility.Visible;
        
        _isNew = isNew;
        if (_isNew) return;

        OriginalProgram = Global.DeepCloneT(program);
        // Manually copy over properties that can't be parsed to JSON (1/2)
        // todo make it possible to convert these properties to JSON (maybe base64 encoding it?) 
        OriginalProgram.Icon = program.Icon;
        OriginalProgram.Process = program.Process;
        OriginalProgram.ActionButton = program.ActionButton;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Save changes
        Global.SavePrograms();
        
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Focus();
        Close();
        Growl.Success("Successfully saved program!");
    }
    
    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Cancel changes

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
        }
        
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Focus();
        Close();
    }

    private void ShowNewIconDialog()
    {
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
            ResetToExecutableIcon();
            return;
        }
        if (dialog.FileName == Program.FilePath)
        {
            ResetToExecutableIcon();
            return;
        }
        
        Program.Icon = Global.GetIconFromFile(dialog.FileName);
        Program.UseExecutableIcon = false;
        Program.IconPath = dialog.FileName;
        ResetIconButton.Visibility = Visibility.Visible;
    }

    private void ResetToExecutableIcon()
    {
        Program ??= new Program();
        Program.Icon = Global.GetIconFromFile(Program.FilePath);
        Program.UseExecutableIcon = true;
        Program.IconPath = "";
        ResetIconButton.Visibility = Visibility.Hidden;
    }

    private void SelectNewIcon_OnClick(object sender, RoutedEventArgs e) => ShowNewIconDialog();

    private void ResetIconButton_OnClick(object sender, RoutedEventArgs e) => ResetToExecutableIcon();
}