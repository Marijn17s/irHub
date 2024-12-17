using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace irHub.Classes;

internal class Settings : INotifyPropertyChanged
{
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion

    public bool StartMinimized { get; set; }
    public bool ShowGarageCover { get; set; }
}