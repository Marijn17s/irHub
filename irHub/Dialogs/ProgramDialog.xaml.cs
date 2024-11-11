using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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
        
        OriginalProgram = program.DeepClone();
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
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Focus();
        Close();
    }
    
    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Cancel changes
        Program = OriginalProgram?.DeepClone();
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Focus();
        Close();
    }
}