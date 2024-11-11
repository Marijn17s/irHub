using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using irHub.Classes.Enums;

namespace irHub.Classes.Models;

public class Program
{
    private const string Start = "START";
    private const string Running = "RUNNING";
    private const string Notfound = "NOT FOUND";
    
    private string? _executableName { get; set; }
    internal string ExecutableName {
        get => _executableName ?? Path.GetFileNameWithoutExtension(FilePath);
        set => _executableName = value;
    }

    public string FilePath { get; set; } = "";
    [JsonIgnore]
    public Image? Icon { get; set; } // todo check image obj type | allow jpeg etc but also support extracting icon from another EXE
    public string Name { get; set; } = "";
    [JsonIgnore]
    public ProgramState State { get; private set; } = ProgramState.Stopped;

    [JsonIgnore]
    public Process? Process { get; set; }
    // todo maybe list of processes
    public string StartArguments { get; set; } = "";
    public bool StartHidden { get; init; }
    public bool IsOverlay { get; set; } // todo Make behaviour different with start hidden etc
    public bool StartWithIracingSim { get; set; } = true;
    public bool StopWithIracingSim { get; set; } = true;
    public bool StartWithIracingUi { get; set; }
    public bool StopWithIracingUi { get; set; }
    
    [JsonIgnore]
    public Button ActionButton { get; set; }

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
        return JsonSerializer.Deserialize<Program>(json, Global.JsonSerializerOptions);
    }

    internal async Task ChangeState(ProgramState state)
    {
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
                        ActionButton.Background = Brushes.LightGray;
                    });
                    break;
                case ProgramState.NotFound:
                default:
                    ActionButton.Dispatcher.BeginInvoke(() =>
                    {
                        ActionButton.Content = Notfound;
                        ActionButton.Background = Brushes.Gray;
                    });
                    break;
            }
        });
    }
}