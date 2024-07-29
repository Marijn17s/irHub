using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using irHub.Classes.Enums;

namespace irHub.Classes.Models;

public class Program
{
    private string? _executableName { get; set; }
    internal string ExecutableName {
        get => _executableName ?? Path.GetFileNameWithoutExtension(FilePath);
        set => _executableName = value;
    }

    internal string FilePath { get; set; } = "";
    internal Image Icon { get; set; } // todo check image obj type | allow jpeg etc but also support extracting icon from another EXE
    internal string Name { get; set; } = "";
    internal ProgramState State { get; set; } = ProgramState.Stopped;

    internal Process? Process { get; set; } = null;
    // todo maybe list of processes
    internal string StartArguments { get; set; } = "";
    internal bool StartHidden { get; set; } = false;
    internal bool IsOverlay { get; set; } // todo Make behaviour different with start hidden etc
    internal bool StartWithIracingSim { get; set; } = true;
    internal bool StopWithIracingSim { get; set; } = true;
    internal bool StartWithIracingUI { get; set; }
    internal bool StopWithIracingUI { get; set; }
    
    internal Button ActionButton { get; set; }
    
    // todo maybe GetProcess() return Process if not null, else search for running processes by the exe name

    internal void ChangeState(ProgramState state)
    {
        if (state is ProgramState.Running)
        {
            State = ProgramState.Running;
            ActionButton.Dispatcher.BeginInvoke(() =>
            {
                ActionButton.Content = "RUNNING";
                ActionButton.Background = Brushes.LightGreen;
                ActionButton.IsEnabled = true;
            });
        }
        else if (state is ProgramState.Stopped)
        {
            State = ProgramState.Stopped;
            ActionButton.Dispatcher.BeginInvoke(() =>
            {
                ActionButton.Content = "START";
                ActionButton.Background = Brushes.LightGray;
                ActionButton.IsEnabled = true;
            });
        }
        else if (state is ProgramState.NotFound)
        {
            State = ProgramState.NotFound;
            ActionButton.Dispatcher.BeginInvoke(() =>
            {
                ActionButton.Content = "NOT FOUND";
                ActionButton.Background = Brushes.Gray;
                ActionButton.IsEnabled = false;
            });
        }
    }
}