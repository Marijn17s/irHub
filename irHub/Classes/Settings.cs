using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace irHub.Classes;

internal class Settings : INotifyPropertyChanged
{
    private bool _startMinimized;
    private bool _showGarageCover;

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (!Global.MainWindowLoaded) return;
            
        Global.SaveSettings();
    }

    #endregion

    [JsonIgnore]
    public string GarageCoverUrl => "http://localhost:8081/irhub";
    
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

    public bool ShowGarageCover
    {
        get => _showGarageCover;
        set
        {
            if (value == _showGarageCover) return;
            _showGarageCover = value;
            OnPropertyChanged();
        }
    }
}