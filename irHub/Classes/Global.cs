﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using Image = System.Windows.Controls.Image;
using MessageBox = HandyControl.Controls.MessageBox;

namespace irHub.Classes;

internal struct Global
{
    private const int MaxRetries = 50;
    private static readonly Image DefaultIcon = new();
    
    private const int SW_HIDE = 0;
    private const int SW_MINIMIZE = 6;

    internal static string BasePath = ""; // todo ?????????

    private static List<Program>? _programs;
    internal static List<Program> Programs
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

    private static List<Program> GetPrograms()
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri("pack://application:,,,/irHub;component/Resources/logo.png");
        bitmap.EndInit();
        DefaultIcon.Source = bitmap;
        
        // todo check continuously if any of the programs start running (maybe not needed?)
        // todo support importing icon from EXE
        // todo make real data with json
        // todo make list of well known applications and quick-add (tab)
        // todo make list of detected simracing related applications
        var programs = new List<Program>();
        var crewchief = new Program
        {
            Name = "Crewchief",
            FilePath = "C:\\Program Files (x86)\\Britton IT Ltd\\CrewChiefV4\\CrewChiefV4.exe",
        };
        GetIcon(crewchief);
        programs.Add(crewchief);
        
        var fanalab = new Program
        {
            Name = "FanaLab",
            FilePath = "C:\\Program Files (x86)\\Fanatec\\FanaLab\\Control\\FanaLab.exe",
        };
        GetIcon(fanalab);
        programs.Add(fanalab);
        
        var vrstelemetry = new Program
        {
            Name = "VRS-TelemetryLogger",
            FilePath = "C:\\Users\\marij\\VirtualRacingSchool\\VRS-TelemetryLogger.exe",
            StartHidden = true,
        };
        GetIcon(vrstelemetry);
        programs.Add(vrstelemetry);
        
        var iOverlay = new Program
        {
            Name = "iOverlay",
            FilePath = "C:\\Program Files\\iOverlay\\iOverlay.exe",
        };
        GetIcon(iOverlay);
        programs.Add(iOverlay);
        
        var onesimracing = new Program
        {
            Name = "1simracing",
            FilePath = "C:\\Users\\marij\\AppData\\Local\\1simracing\\1simracing.exe",
            StartHidden = true,
        };
        GetIcon(onesimracing);
        programs.Add(onesimracing);
        
        var irsidekick = new Program
        {
            Name = "irSidekickLivery",
            FilePath = "C:\\Users\\marij\\AppData\\Local\\Programs\\irSidekick\\Livery\\irSidekickLivery.exe",
            StartHidden = true,
        };
        GetIcon(irsidekick);
        programs.Add(irsidekick);
        
        var garage61 = new Program
        {
            Name = "Garage 61",
            FilePath = "C:\\Users\\marij\\AppData\\Roaming\\garage61-install\\garage61-launcher.exe",
        };
        GetIcon(garage61);
        programs.Add(garage61);

        var racelabapps = new Program
        {
            Name = "RaceLabApps",
            FilePath = "C:\\Users\\marij\\AppData\\Local\\racelabapps\\RacelabApps.exe",
        };
        GetIcon(racelabapps);
        programs.Add(racelabapps);

        return programs;
    }

    internal static bool IsProgramRunning(Program program)
    {
        var existingProcess = Process.GetProcesses().FirstOrDefault(process => process.ProcessName == program.ExecutableName);
        if (existingProcess is null) return false;
        
        program.Process = existingProcess;
        if (program.ExecutableName != existingProcess.ProcessName)
            program.ExecutableName = existingProcess.ProcessName;

        AddProcessEventHandlers(program, program.Process);
        return true;
    }

    private static void AddProcessEventHandlers(Program program, Process process)
    {
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
                MessageBox.Show("Failed to start " + program.ExecutableName); // todo
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

    private static void GetIcon(Program program)
    {
        if (!File.Exists(program.FilePath))
        {
            program.Icon = DefaultIcon;
            return;
        }
        
        var bitmap = Icon.ExtractAssociatedIcon(program.FilePath)?.ToBitmap();
        if (bitmap is null)
        {
            program.Icon = DefaultIcon;
            return;
        }

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();

        program.Icon = new Image { Source = bitmapImage };
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
    
    #region DLLImports
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    #endregion
}