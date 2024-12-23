using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using iRacingSdkWrapper;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Helpers;
using Image = System.Windows.Controls.Image;
using MessageBox = HandyControl.Controls.MessageBox;
// ReSharper disable InconsistentNaming
// ReSharper disable EventUnsubscriptionViaAnonymousDelegate
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace irHub.Classes;

internal struct Global
{
    private const int MaxRetries = 50;
    internal static readonly Image DefaultIcon = new();
    
    private const int SW_HIDE = 0;
    private const int SW_MINIMIZE = 6;
    
    internal static string irHubDirectoryPath = "";
    internal static bool ShouldShowGarageCover = false;
    internal static bool NeedsProgramRefresh;
    internal static bool CancelStateCheck = false;
    internal static bool CancelIracingUiStateCheck = false;

    internal static readonly SdkWrapper iRacingClient = new();
    internal static Settings Settings = new();

    internal static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };
    
    internal static readonly BlurEffect WindowBlurEffect = new()
    {
        Radius = 10,
    };

    private static ObservableCollection<Program>? _programs;
    internal static ObservableCollection<Program> Programs
    {
        get
        {
            if (_programs is null || _programs.Count is 0)
                _programs = GetPrograms();
            return _programs;
        }
        set
        {
            if (value is null) return;
            _programs = value;
        }
    }

    private static ObservableCollection<Program> GetPrograms()
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri("pack://application:,,,/irHub;component/Resources/logo.png");
        bitmap.EndInit();
        DefaultIcon.Source = bitmap;
        
        // todo make list of well known applications and quick-add (tab)
        // todo make list of detected simracing related applications
        var programs = new ObservableCollection<Program>();
        
        var json = File.ReadAllText(Path.Combine(irHubDirectoryPath, "programs.json"));
        if (!IsValidJson(json))
            return programs;
        programs = JsonSerializer.Deserialize<ObservableCollection<Program>>(json);
        if (programs is null || programs.Count is 0)
            return [];

        foreach (var program in programs)
        {
            if (!program.UseExecutableIcon)
            {
                program.Icon = IconHelper.GetIconFromFile(program.IconPath);
                continue;
            }
            program.Icon = IconHelper.GetIconFromFile(program.FilePath);
        }
        return programs;
    }

    internal static void RefreshPrograms()
    {
        Programs = GetPrograms();
        NeedsProgramRefresh = true;
    }

    internal static void SavePrograms()
    { 
        var json = JsonSerializer.Serialize(Programs, JsonSerializerOptions);
        File.WriteAllText(Path.Combine(irHubDirectoryPath, "programs.json"), json);
    }
    
    internal static void LoadSettings()
    {
        var json = File.ReadAllText(Path.Combine(irHubDirectoryPath, "settings.json"));
        if (json is "{}")
        {
            SaveSettings();
            return;
        }
        
        if (!IsValidJson(json))
            return;
        
        Settings = JsonSerializer.Deserialize<Settings>(json) ?? Settings;
    }

    internal static void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings, JsonSerializerOptions);
        File.WriteAllText(Path.Combine(irHubDirectoryPath, "settings.json"), json);
    }
    
    private static bool IsValidJson(string source)
    {
        try
        {
            using var doc = JsonDocument.Parse(source);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
    
    public static T? DeepCloneT<T>(T obj)
    {
        // Deep clone any type of object
        string json = JsonSerializer.Serialize(obj, JsonSerializerOptions);
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
    }
    
    internal static void CopyProperties(Program source, Program destination)
    {
        // Use reflection to copy over each property
        foreach (var property in typeof(Program).GetProperties())
            if (property.CanWrite)
                property.SetValue(destination, property.GetValue(source));
    }
    
    internal static async Task CheckProgramsRunning()
    {
        // Check all programs if they're running
        var processes = Process.GetProcesses();

        foreach (var program in Programs)
        {
            if (program.State is ProgramState.NotFound && File.Exists(program.FilePath))
                await program.ChangeState(ProgramState.Stopped);
            
            var existingProcess = processes.FirstOrDefault(process => process.ProcessName == program.ExecutableName);
            if (existingProcess is null || existingProcess.HasExited) continue;
            
            program.Process = existingProcess;
            if (program.ExecutableName != existingProcess.ProcessName)
                program.ExecutableName = existingProcess.ProcessName;

            await program.ChangeState(ProgramState.Running);
            AddProcessEventHandlers(program, program.Process);
        }
    }

    internal static bool IsProgramRunning(Program program)
    {
        // Check if program is running
        var existingProcess = Process.GetProcesses().FirstOrDefault(process => process.ProcessName == program.ExecutableName);
        if (existingProcess is null || existingProcess.HasExited) return false;
        
        program.Process = existingProcess;
        if (program.ExecutableName != existingProcess.ProcessName)
            program.ExecutableName = existingProcess.ProcessName;

        AddProcessEventHandlers(program, program.Process);
        return true;
    }

    private static void AddProcessEventHandlers(Program program, Process? process)
    {
        // Add event handlers like the process exiting
        if (process is null || process.HasExited) return;
        
        process.EnableRaisingEvents = true;
        process.Exited += async (_, _) =>
        {
            await Task.Delay(1000);
            var processes = Process.GetProcessesByName(program.ExecutableName);
            if (processes.Length is not 0)
                return;
            await program.ChangeState(ProgramState.Stopped);
        };
    }
    
    internal static async Task<bool> StartProgram(Program program)
    {
        // Check if the program is already running
        if (IsProgramRunning(program))
        {
            await program.ChangeState(ProgramState.Running);
            return true;
        }

        var startInfo = GetApplicationStartInfo(program);
        if (startInfo is null)
        {
            await program.ChangeState(ProgramState.NotFound);
            return false;
        }
        
        var process = Process.Start(startInfo);
        await Task.Delay(200);
        
        if (process is null || process.HasExited)
        {
            var processes = Process.GetProcessesByName(program.ExecutableName);
            if (processes.Length < 1)
            {
                MessageBox.Show("Failed to start " + program.ExecutableName);
                return false;
            }
            process = processes[0];
        }
        
        if (program.ExecutableName != process.ProcessName)
            program.ExecutableName = process.ProcessName;
        
        AddProcessEventHandlers(program, process);
        
        PostApplicationStartLogic(process);

        await program.ChangeState(ProgramState.Running);
        program.Process = process;
        return true;
    }
    
    internal static async Task StopProgram(Program program)
    {
        if (program.Process is null || program.Process.HasExited)
        {
            if (program.FilePath is "") return;
            
            KillProcessesByPartialName(program.ExecutableName);
            return;
        }
        
        var processName = program.Process.ProcessName;
        program.Process.Exited -= (_, _) => {};
        program.Process.Close();

        KillProcessesByPartialName(processName);

        const string g61 = "garage61";
        if (processName.Contains(g61, StringComparison.InvariantCultureIgnoreCase))
            KillProcessesByPartialName(g61);

        program.Process = null;
        await program.ChangeState(ProgramState.Stopped);
    }

    private static ProcessStartInfo? GetApplicationStartInfo(Program program)
    {
        var startInfo = new ProcessStartInfo();
        
        if (program.ExecutableName.Contains("racelab", StringComparison.InvariantCultureIgnoreCase))
        {
            // todo check if doesn't contain app!!!!!!!!!!!!!!!!!
            var fi = new FileInfo(program.FilePath);
            if (!fi.Exists) return null;
            
            var fvi = FileVersionInfo.GetVersionInfo(fi.FullName);
            var exeName = fi.Name;
            var exeDir = $"{fi.DirectoryName}\\app-{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}\\";

            startInfo.FileName = exeDir + exeName;
            startInfo.WorkingDirectory = exeDir;
        }
        else
        {
            if (!File.Exists(program.FilePath)) return null;
            
            var fi = new FileInfo(program.FilePath);
            if (!fi.Exists) return null;
            
            startInfo.FileName = program.FilePath;
            startInfo.WorkingDirectory = fi.DirectoryName;

            if (program.StartHidden)
            {
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
        }
        
        startInfo.Arguments = program.StartArguments;
        
        return startInfo;
    }

    private static async void PostApplicationStartLogic(Process process)
    {
        /*const string racelab = "RaceLabApps";
        if (process.ProcessName.Contains(racelab, StringComparison.InvariantCultureIgnoreCase))
        {
            
        }*/

        const string onesim = "1simracing";
        if (process.ProcessName.Contains(onesim, StringComparison.InvariantCultureIgnoreCase))
        {
            var hWnd = IntPtr.Zero;
            int retries = 0;
            
            while ((hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) && retries <= MaxRetries)
            {
                hWnd = FindWindow(null, process.ProcessName);
                await Task.Delay(200);

                retries++;
            }

            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_MINIMIZE);
                ShowWindow(hWnd, SW_HIDE);
            }
        }

        // Use if an application requires closing the window instead of minimizing it to get it to tray
        /*var hWnd = IntPtr.Zero;
            int retries = 0;

            while ((hWnd == IntPtr.Zero && retries < MaxRetries))
            {
                await Task.Delay(50);

                process.Refresh();
                hWnd = process.MainWindowHandle;

                retries++;
            }

            if (hWnd != IntPtr.Zero)
                SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);*/
    }
    
    internal static void ApplicationSpecificStopLogic(Program program, Process? process = null)
    {
        
    }
    
    internal static bool IsFile(string path) => !File.GetAttributes(path).HasFlag(FileAttributes.Directory);

    internal static Image GetIconFromFile(string path)
    {
        if (!File.Exists(path) || !IsFile(path))
            return DefaultIcon;
        
        var fileExtension = Path.GetExtension(path);
        if (fileExtension is ".exe")
            return GetIconFromExe(path);
        
        using var bitmap = new Bitmap(path);
        if (bitmap is { Height: 0, Width: 0 })
            return DefaultIcon;
        
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;
        
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        
        return new Image { Source = bitmapImage };
    }

    private static Image GetIconFromExe(string path)
    {
        if (!File.Exists(path))
            return DefaultIcon;
        
        var bitmap = Icon.ExtractAssociatedIcon(path)?.ToBitmap();
        if (bitmap is null)
            return DefaultIcon;

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();

        return new Image { Source = bitmapImage };
    }
    
    #region Processes
    internal static Process? FindProcess()
    {
        var currentProcess = Process.GetCurrentProcess();
        Process[] procs = Process.GetProcessesByName(currentProcess.ProcessName);
        return procs.FirstOrDefault(process => process.Id != currentProcess.Id);
    }
    
    internal static void FocusProcess(Process process)
    {
        var hWnd = process.MainWindowHandle;
        ShowWindow(hWnd, 0x09); // 0x09 = restore window state
        SetForegroundWindow(hWnd);
    }
    
    internal static List<Process> GetProcessesByPartialName(string name)
    {
        return Process.GetProcesses()
            .Where(x => x.ProcessName.Contains(name, StringComparison.InvariantCultureIgnoreCase))
            .ToList();
    }
    
    internal static void KillProcessesByPartialName(string name)
    {
        Process.GetProcesses()
            .Where(x => x.ProcessName.Contains(name, StringComparison.InvariantCultureIgnoreCase))
            .ToList()
            .ForEach(x => x.Kill());
    }
    #endregion
    
    #region DLLImports
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr SetForegroundWindow(IntPtr hWnd);
    #endregion
}