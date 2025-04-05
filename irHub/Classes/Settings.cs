using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace irHub.Classes;

internal class Settings : INotifyPropertyChanged
{
    private bool _startMinimized;

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (!Global.MainWindowLoaded) return;
            
        Global.SaveSettings();
    }

    #endregion
    
    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            if (value == _startMinimized) return;
            _startMinimized = value;
            OnPropertyChanged();
        }
    }
}