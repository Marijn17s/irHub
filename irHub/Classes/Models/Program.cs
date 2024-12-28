using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using irHub.Classes.Enums;
using Serilog;

namespace irHub.Classes.Models;

public class Program : INotifyPropertyChanged
{
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
    
    private const string Start = "START";
    private const string Running = "RUNNING";
    private const string Notfound = "NOT FOUND";
    
    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
        }
    }
    
    private string? _executableName { get; set; }
    internal string ExecutableName {
        get => _executableName ?? Path.GetFileNameWithoutExtension(FilePath);
        set => _executableName = value;
    }

    private string _filePath = "";

    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    private Image? _icon;

    [JsonIgnore]
    public Image? Icon
    {
        get => _icon;
        set
        {
            if (_icon == value) return;
            _icon = value;
            OnPropertyChanged();
        }
    }

    private string _iconPath = "";
    public string IconPath
    {
        get
        {
            if (_iconPath is "" || UseExecutableIcon)
                return FilePath;
            return _iconPath;
        }
        set
        {
            _iconPath = value;
            OnPropertyChanged();
        }
    }
    
    public bool UseExecutableIcon { get; set; } = true;
    
    [JsonIgnore]
    public ProgramState State { get; private set; } = ProgramState.Stopped;

    [JsonIgnore]
    public Process? Process { get; set; }
    
    // todo maybe list of processes
    
    public string StartArguments { get; set; } = "";
    public bool StartHidden { get; set; }
    public bool IsOverlay { get; set; } // todo Make behaviour different with start hidden etc
    public bool StartWithIracingUi { get; set; }
    public bool StopWithIracingUi { get; set; }
    public bool StartWithIracingSim { get; set; } = true;
    public bool StopWithIracingSim { get; set; } = true;
    
    [JsonIgnore]
    public Button ActionButton { get; set; } = null!;

    internal Process? GetProcess()
    {
        if (Process is not null) return Process;

        var processes = Global.GetProcessesByPartialName(ExecutableName);
        if (processes.Count > 0) return processes[0];

        return null;
    }
    
    public Program DeepClone()
    {
        string json = JsonSerializer.Serialize(this, Global.JsonSerializerOptions);
        return JsonSerializer.Deserialize<Program>(json, Global.JsonSerializerOptions) ?? new Program();
    }

    internal async Task ChangeState(ProgramState state)
    {
        Log.Information($"{Process?.ProcessName} changing state from {State} to {state}");
        State = state;
        
        await Task.Run(() =>
        {
            switch (state)
            {
                case ProgramState.Running:
                    ActionButton.Dispatcher.BeginInvoke(() =>
                    {
                        ActionButton.Content = Running;
                        ActionButton.Background = Brushes.LightGreen;
                    });
                    break;
                case ProgramState.Stopped:
                    ActionButton.Dispatcher.BeginInvoke(() =>
                    {
                        ActionButton.Content = Start;
                        ActionButton.Background = new SolidColorBrush(Color.FromRgb(55, 58, 62));
                    });
                    break;
                case ProgramState.NotFound:
                default:
                    ActionButton.Dispatcher.BeginInvoke(() =>
                    {
                        ActionButton.Content = Notfound;
                        ActionButton.Background = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                    });
                    break;
            }
        });
    }
}