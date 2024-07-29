using System.ComponentModel;
using System.Runtime.CompilerServices;
using irHub.Classes.Models;

namespace irHub.Dialogs;

public partial class ProgramDialog
{
    private Program _program;
    public Program Program
    {
        get => _program;
        set
        {
            _program = value;
            OnPropertyChanged();
        }
    }
    
    internal ProgramDialog(ref Program program)
    {
        InitializeComponent();

        Program = program;
    }
    
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
}