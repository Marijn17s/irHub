using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Windows;
using Serilog;

namespace irHub.Dialogs;

public partial class ProfileNameDialog : INotifyPropertyChanged
{
    private bool IsNew { get; }
    
    private string _originalProfileName;
    public string OriginalProfileName
    {
        get => _originalProfileName;
        set
        {
            _originalProfileName = value;
            OnPropertyChanged();
        }
    }
    
    private string _profileName;
    public string ProfileName
    {
        get => _profileName;
        set
        {
            _profileName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValidName));
        }
    }
    
    public bool IsValidName => Global.IsValidProfileName(ProfileName);
    
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
    
    internal ProfileNameDialog(string profileName = "")
    {
        InitializeComponent();
        
        IsNew = profileName is "";
        OriginalProfileName = profileName;
        
        Log.Information($"Opening profile name dialog. New: {IsNew}" + (IsNew ? "" : $", Profile: '{profileName}'"));
        if (Application.Current.MainWindow is MainWindow mainWindow)
            Owner = mainWindow;
        
        ProfileName = profileName;
        DataContext = this;

        ProfileNameTextBox.Focus();
        ProfileNameTextBox.CaretIndex = profileName.Length;
    }

    private void ProfileNameDialog_OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            Log.Debug("User pressed Escape, canceling profile dialog");
            DialogResult = false;
            Close();
            return;
        }
        if (e.Key is not Key.Enter) return;

        if (!Global.IsValidProfileName(ProfileName))
        {
            Log.Warning($"User attempted to confirm invalid profile name: '{ProfileName}'");
            Growl.Warning("Profile name contains invalid characters. Please use only letters, numbers, spaces, and common punctuation.");
            return;
        }

        if (!IsNew)
        {
            Log.Information($"User confirmed rename profile from '{OriginalProfileName}' to '{ProfileName}'");
            var success = Global.RenameProfile(OriginalProfileName, ProfileName);
            if (success)
            {
                DialogResult = true;
                Close();
            }
            return;
        }
        
        Log.Information($"User confirmed create new profile with name '{ProfileName}'");
        var created = Global.CreateProfile(ProfileName);
        if (created)
        {
            DialogResult = true;
            Close();
        }
    }
}