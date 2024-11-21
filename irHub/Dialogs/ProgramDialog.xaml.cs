using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Models;
using irHub.Windows;

namespace irHub.Dialogs;

public partial class ProgramDialog : INotifyPropertyChanged
{
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
    
    internal ProgramDialog(ref Program program)
    {
        InitializeComponent();
        if (Application.Current.MainWindow is MainWindow mainWindow)
            Owner = mainWindow;

        OriginalProgram = Global.DeepCloneT(program);
        // Manually copy over properties that can't be parsed to JSON (1/2)
        // todo make it possible to convert these properties to JSON (maybe base64 encoding it?) 
        OriginalProgram.Icon = program.Icon;
        OriginalProgram.Process = program.Process;
        OriginalProgram.ActionButton = program.ActionButton;
        
        Program = program;
        DataContext = Program;
    }
    
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion

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
        if (OriginalProgram is not null && Program is not null)
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
}