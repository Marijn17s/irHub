using System.ComponentModel;
using System.Runtime.CompilerServices;
using irHub.Helpers;

namespace irHub.Classes;

internal class Settings : INotifyPropertyChanged
{
    private bool _startMinimized;
    private bool _startWithWindows;
    private bool _enableGlobalHotkey = true;
    
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
    
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (value == _startWithWindows) return;
            _startWithWindows = value;
            OnPropertyChanged();
            
            if (Global.MainWindowLoaded)
            {
                if (value)
                    StartupHelper.EnableStartup();
                else
                    StartupHelper.DisableStartup();
            }
        }
    }
    
    public bool EnableGlobalHotkey
    {
        get => _enableGlobalHotkey;
        set
        {
            if (value == _enableGlobalHotkey) return;
            _enableGlobalHotkey = value;
            OnPropertyChanged();
        }
    }
}