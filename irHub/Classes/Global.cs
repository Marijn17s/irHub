using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using iRacingSdkWrapper;
using irHub.Classes.Enums;
using irHub.Classes.Models;
using irHub.Helpers;
using Serilog;
using Image = System.Windows.Controls.Image;
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
    internal static string ProfilesPath = "";
    internal static bool MainWindowLoaded = false;
    internal static bool NeedsProgramRefresh;
    internal static bool CancelStateCheck = false;
    internal static bool CancelIracingUiStateCheck = false;
    internal static bool StartMinimizedArgument = false;
    
    internal static event EventHandler? ProfilesChanged;

    internal static readonly SdkWrapper iRacingClient = new();
    public static Settings Settings = new();

    internal static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };
    
    internal static readonly BlurEffect WindowBlurEffect = new()
    {
        Radius = 10,
    };

    private static void OnProfilesChanged()
    {
        Log.Debug("Profiles changed event raised, notifying UI");
        ProfilesChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static void InitializeDefaultProfile()
    {
        Log.Information("Initializing default profile system");
        EnsureValidDefaultProfile();
    }

    internal static void SetDefaultProfile(string profile)
    {
        Log.Information($"Setting '{profile}' as default profile");
        
        if (!IsValidProfileName(profile))
        {
            Log.Warning($"Profile name '{profile}' contains invalid characters or is reserved");
            Growl.Warning("Profile name contains invalid characters");
            return;
        }
        
        var profilePath = Path.Combine(ProfilesPath, profile);
        if (!Directory.Exists(profilePath))
        {
            Log.Warning($"Profile '{profile}' does not exist, cannot set as default");
            Growl.Warning("Profile does not exist");
            return;
        }
        
        if (Settings.DefaultProfile == profile)
        {
            Log.Debug($"Profile '{profile}' is already the default profile");
            return;
        }
        
        var previousDefault = Settings.DefaultProfile;
        Settings.DefaultProfile = profile;
        
        Log.Information($"Successfully set '{profile}' as default profile (was '{previousDefault}')");
        Growl.Success($"'{profile}' is now the default profile");
        
        // Refresh the UI to show the new default profile
        OnProfilesChanged();
    }

    private static void CreateDefaultProfile()
    {
        var defaultProfileName = "default profile";
        var defaultProfilePath = Path.Combine(ProfilesPath, defaultProfileName);
        Directory.CreateDirectory(defaultProfilePath);
        File.WriteAllText(Path.Combine(defaultProfilePath, "programs.json"), "[]");
        
        RefreshProfiles();
        SelectedProfile = defaultProfileName;
        RefreshPrograms();
        
        Log.Information($"Created new default profile '{defaultProfileName}'");
        Growl.Info("Created new default profile since all profiles were deleted");
    }

    private static void EnsureValidDefaultProfile()
    {
        Log.Debug("Ensuring valid default profile is selected");
        
        var profiles = GetProfiles();
        
        // If no profiles exist, create default profile
        if (profiles.Count is 0)
        {
            Log.Information("No profiles exist, creating default profile");
            CreateDefaultProfile();
            Settings.DefaultProfile = "default profile";
            return;
        }
        
        // If only one profile exists, make it the default
        if (profiles.Count is 1)
        {
            var singleProfile = profiles[0];
            Log.Information($"Only one profile exists ('{singleProfile}'), setting as default");
            Settings.DefaultProfile = singleProfile;
            SelectedProfile = singleProfile;
            RefreshPrograms();
            return;
        }
        
        // Multiple profiles exist, check if configured default exists
        if (profiles.Contains(Settings.DefaultProfile))
        {
            Log.Debug($"Configured default profile '{Settings.DefaultProfile}' exists, using it");
            SelectedProfile = Settings.DefaultProfile;
            RefreshPrograms();
            return;
        }
        
        // Configured default doesn't exist, fallback to "default profile"
        Log.Warning($"Configured default profile '{Settings.DefaultProfile}' doesn't exist, checking for fallback");
        
        if (profiles.Contains("default profile"))
        {
            Log.Information("Using fallback 'default profile'");
            Settings.DefaultProfile = "default profile";
            SelectedProfile = "default profile";
            RefreshPrograms();
            return;
        }
        
        // Even fallback doesn't exist, use first available profile
        var firstProfile = profiles[0];
        Log.Warning($"Neither configured default nor fallback exist, using first available profile '{firstProfile}'");
        Settings.DefaultProfile = firstProfile;
        SelectedProfile = firstProfile;
        RefreshPrograms();
    }

    internal static bool IsValidProfileName(string profileName)
    {
        // Validate profile names against filesystem restrictions
        if (string.IsNullOrWhiteSpace(profileName))
            return false;
            
        // Check for invalid filesystem characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (profileName.Any(c => invalidChars.Contains(c)))
            return false;
            
        // Check for reserved names (Windows filesystem restrictions)
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(profileName);
        if (reservedNames.Contains(nameWithoutExtension.ToUpperInvariant()))
            return false;
            
        return true;
    }

    private static ObservableCollection<Program>? _programs;
    internal static ObservableCollection<Program> Programs
    {
        get
        {
            if (_programs is null)
                _programs = GetPrograms();
            return _programs;
        }
        set
        {
            if (value is null) return;
            _programs = value;
        }
    }
    
    private static ObservableCollection<string>? _profiles;
    internal static ObservableCollection<string> Profiles
    {
        get
        {
            if (_profiles is null || _profiles.Count is 0)
                _profiles = GetProfiles();
            return _profiles;
        }
        set
        {
            if (value is null) return;
            _profiles = value;
        }
    }
    
    private static string? _selectedProfile;
    internal static string SelectedProfile
    {
        get
        {
            if (_selectedProfile is null)
            {
                EnsureValidDefaultProfile();
                return _selectedProfile ?? "";
            }
            return _selectedProfile;
        }
        set
        {
            if (value is null) return;
            _selectedProfile = value.Trim();
        }
    }

    private static ObservableCollection<Program> GetPrograms()
    {
        // todo wordt vaker dan 1x uitgevoerd met startup????
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri("pack://application:,,,/irHub;component/Resources/logo.png");
        bitmap.EndInit();
        DefaultIcon.Source = bitmap;
        
        // todo make list of well known applications and quick-add (tab)
        // todo make list of detected simracing related applications
        var programs = new ObservableCollection<Program>();
        
        // Check if the selected profile exists
        if (string.IsNullOrEmpty(SelectedProfile))
        {
            Log.Warning("No profile selected, returning empty programs list");
            return programs;
        }
        
        var profilePath = Path.Combine(ProfilesPath, SelectedProfile);
        if (!Directory.Exists(profilePath))
        {
            Log.Warning($"Selected profile '{SelectedProfile}' does not exist, returning empty programs list");
            return programs;
        }
        
        var programsJsonPath = Path.Combine(profilePath, "programs.json");
        if (!File.Exists(programsJsonPath))
        {
            Log.Warning($"Programs.json not found for profile '{SelectedProfile}', creating empty file");
            File.WriteAllText(programsJsonPath, "[]");
            return programs;
        }
        
        string json;
        try
        {
            json = File.ReadAllText(programsJsonPath);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to read programs.json for profile '{SelectedProfile}': {ex.Message}");
            return programs;
        }
        
        if (!IsValidJson(json))
        {
            Log.Warning($"Invalid JSON in programs.json for profile '{SelectedProfile}', returning empty programs list");
            return programs;
        }
        
        programs = JsonSerializer.Deserialize<ObservableCollection<Program>>(json);
        if (programs is null || programs.Count is 0)
            return [];

        Log.Information($"{programs.Count} programs loaded from profile '{SelectedProfile}'.");
        
        foreach (var program in programs)
        {
            program.Icon = IconHelper.GetIconFromFile(program.IconPath);
            if (program.Icon == DefaultIcon && File.Exists(program.FilePath))
                program.SetExecutableIcon();
        }
        
        Log.Information("Loaded initial programs settings");
        return programs;
    }

    internal static void RefreshPrograms()
    {
        Log.Information("Refreshing programs..");
        
        Programs = GetPrograms();
        NeedsProgramRefresh = true;
    }

    internal static void SavePrograms()
    {
        Log.Information($"Saving application settings for profile '{SelectedProfile}'..");
        
        var json = JsonSerializer.Serialize(Programs, JsonSerializerOptions);
        var profileProgramsPath = Path.Combine(ProfilesPath, SelectedProfile, "programs.json");
        File.WriteAllText(profileProgramsPath, json);
        
        Log.Information($"Saved programs for profile '{SelectedProfile}'");
    }

    private static ObservableCollection<string> GetProfiles()
    {
        // Load all profiles
        if (!Directory.Exists(ProfilesPath))
        {
            Log.Debug("Profiles directory does not exist, creating it");
            Directory.CreateDirectory(ProfilesPath);
            return [];
        }
        
        var directories = Directory.GetDirectories(ProfilesPath);
        if (directories is not null && directories.Length > 0)
        {
            var folderNames = directories
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .Cast<string>()
                .ToList();
            
            Log.Information($"Found {folderNames.Count} profile(s): {string.Join(", ", folderNames)}");
            return new ObservableCollection<string>(folderNames);
        }
        
        Log.Warning("No profiles found in profiles directory");
        return [];
    }

    internal static void RefreshProfiles()
    {
        Profiles = GetProfiles();
    }

    internal static bool CreateProfile(string profile, string referenceProfile = "")
    {
        Log.Information(string.IsNullOrWhiteSpace(referenceProfile)
            ? $"Creating new profile '{profile}'"
            : $"Duplicating profile '{referenceProfile}'");

        if (!IsValidProfileName(profile))
        {
            Log.Warning($"Profile name '{profile}' contains invalid characters or is reserved");
            Growl.Warning("Profile name contains invalid characters. Please use only letters, numbers, spaces, and common punctuation.");
            return false;
        }
        
        var exists = Directory.Exists(Path.Combine(ProfilesPath, profile));
        if (exists)
        {
            Log.Warning($"Profile '{profile}' already exists, cannot create duplicate");
            Growl.Warning("Profile name is already taken. Please choose another name.");
            return false;
        }
        
        var newProfilePath = Path.Combine(ProfilesPath, profile);
        Directory.CreateDirectory(newProfilePath);
        Log.Debug($"Created profile directory: {newProfilePath}");

        RefreshProfiles();
        
        if (referenceProfile is not "")
        {
            var referenceProfilePath = Path.Combine(ProfilesPath, referenceProfile);
            if (!Directory.Exists(referenceProfilePath))
            {
                Log.Warning($"Reference profile '{referenceProfile}' does not exist, cannot duplicate from it");
                Growl.Warning($"Profile '{referenceProfile}' does not exist, cannot duplicate from it");
                Directory.Delete(newProfilePath, true);
                return false;
            }
            
            var referenceProgramsPath = Path.Combine(referenceProfilePath, "programs.json");
            if (!File.Exists(referenceProgramsPath))
            {
                Log.Warning($"Programs.json not found in reference profile '{referenceProfile}', creating empty file");
                File.WriteAllText(Path.Combine(newProfilePath, "programs.json"), "[]");
                Growl.Warning($"Applications config not found in profile '{referenceProfile}', creating empty file");
                Directory.Delete(newProfilePath, true);
                return false;
            }
            
            Log.Debug($"Duplicating profile '{referenceProfile}'");
            var referenceContents = File.ReadAllText(referenceProgramsPath);
            File.WriteAllText(Path.Combine(newProfilePath, "programs.json"), referenceContents);
            
            OnProfilesChanged();
            Log.Information($"Profile '{referenceProfile}' successfully duplicated");
            Growl.Success("Profile successfully duplicated");
            return true;
        }
        
        Log.Debug($"Creating empty programs.json for new profile '{profile}'");
        File.WriteAllText(Path.Combine(newProfilePath, "programs.json"), "[]");
        OnProfilesChanged();
        Log.Information($"Profile '{profile}' created successfully");
        Growl.Success("Profile successfully created");
        
        // Ensure valid default profile after creating new profile
        EnsureValidDefaultProfile();
        return true;
    }

    internal static void SwitchToProfile(string profile)
    {
        Log.Information($"Switching from '{SelectedProfile}' to '{profile}'");
        
        if (!IsValidProfileName(profile))
        {
            Log.Warning($"Profile name '{profile}' is invalid, cannot switch to it");
            Growl.Warning("Profile name is invalid, cannot switch to it");
            return;
        }
        
        var profilePath = Path.Combine(ProfilesPath, profile);
        if (!Directory.Exists(profilePath))
        {
            Log.Warning("Profile does not exist, cannot switch to it");
            return;
        }

        if (!string.IsNullOrEmpty(SelectedProfile))
        {
            Log.Debug($"Saving current programs for profile '{SelectedProfile}' before switching profile");
            var json = JsonSerializer.Serialize(Programs, JsonSerializerOptions);
            var currentProfilePath = Path.Combine(ProfilesPath, SelectedProfile, "programs.json");
            File.WriteAllText(currentProfilePath, json);
            Log.Information($"Saved programs for profile '{SelectedProfile}'");
        }

        SelectedProfile = profile;
        OnProfilesChanged();
        
        RefreshPrograms();
        
        Log.Information($"Successfully switched to profile '{profile}'");
        Growl.Success($"Switched to profile '{profile}'");
    }
    
    internal static bool RenameProfile(string oldName, string newName)
    {
        Log.Information($"Renaming profile from '{oldName}' to '{newName}'");
        
        if (!IsValidProfileName(oldName) || !IsValidProfileName(newName))
        {
            Log.Warning($"Profile name is invalid, cannot rename it - old: '{oldName}', new: '{newName}'");
            Growl.Warning("The profile name you chose to rename is invalid. Please use only letters, numbers, spaces, and common punctuation.");
            return false;
        }
        
        var oldPath = Path.Combine(ProfilesPath, oldName);
        var newPath = Path.Combine(ProfilesPath, newName);
        
        var oldExists = Directory.Exists(oldPath);
        if (!oldExists)
        {
            Log.Warning($"Profile '{oldName}' does not exist, cannot rename it");
            Growl.Error("The profile you chose to rename does not exist.");
            return false;
        }
        
        var newExists = Directory.Exists(newPath);
        if (newExists)
        {
            Log.Warning($"Profile '{newName}' is already taken, cannot rename to existing name");
            Growl.Warning("Profile name is already taken. Please choose another name.");
            return false;
        }
        
        try
        {
            Log.Debug($"Moving profile directory from '{oldPath}' to '{newPath}'");
            Directory.Move(oldPath, newPath);
            
            // If we renamed the currently selected profile, update the selection
            if (SelectedProfile == oldName)
            {
                Log.Debug($"Updating selected profile from '{oldName}' to '{newName}'");
                SelectedProfile = newName;
                RefreshPrograms();
            }
            
            OnProfilesChanged();
            Log.Information($"Successfully renamed profile '{oldName}' to '{newName}'");
            Growl.Success("Profile successfully renamed");
            
            // Ensure valid default profile after renaming
            EnsureValidDefaultProfile();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to rename profile from '{oldName}' to '{newName}': {ex.Message}");
            Growl.Error($"Failed to rename profile: {ex.Message}");
            return false;
        }
    }

    internal static bool DuplicateProfile(string profile)
    {
        Log.Information($"Duplicating profile '{profile}'");
        
        if (!IsValidProfileName(profile))
        {
            Log.Warning($"Profile name '{profile}' contains invalid characters or is reserved");
            Growl.Warning("Profile name contains invalid characters. Please use only letters, numbers, spaces, and common punctuation.");
            return false;
        }
        
        var profilePath = Path.Combine(ProfilesPath, profile);
        if (!Directory.Exists(profilePath))
        {
            Log.Warning($"Profile '{profile}' does not exist, cannot duplicate it");
            Growl.Warning("Profile does not exist, cannot duplicate it");
            return false;
        }
        
        string newProfile = $"{profile} (duplicate)";
        Log.Debug($"Creating duplicate profile with name '{newProfile}'");
        var result = CreateProfile(newProfile, profile);
        if (!result)
        {
            Log.Error($"Failed to duplicate profile '{profile}'");
            Growl.Error($"Failed to duplicate profile '{profile}'");
        }
        return result;
    }
    
    internal static void DeleteProfile(string profile)
    {
        Log.Information($"Deleting profile '{profile}'");
        
        if (!IsValidProfileName(profile))
        {
            Log.Warning($"Profile name '{profile}' is invalid, cannot delete it");
            Growl.Warning("Profile name contains invalid characters. Please try deleting it in your documents folder by deleting the folder of this profile.");
            return;
        }
        
        var path = Path.Combine(ProfilesPath, profile);
        if (Directory.Exists(path))
        {
            Log.Debug($"Deleting profile directory: {path}");
            Directory.Delete(path, true);
            
            // If we deleted the currently selected profile, select the first available one
            if (SelectedProfile == profile)
            {
                Log.Debug($"Deleted profile '{profile}' was the currently selected profile, switching to default profile");
                EnsureValidDefaultProfile();
            }
            
            OnProfilesChanged();
            Log.Information($"Successfully deleted profile '{profile}'");
            Growl.Success("Profile successfully deleted");
            return;
        }
        
        Log.Warning($"Profile '{profile}' not found, cannot delete it");
        Growl.Warning("Profile not found");
    }
    
    internal static void LoadSettings()
    {
        // Load application settings
        Log.Information("Loading application settings..");
        
        var json = File.ReadAllText(Path.Combine(irHubDirectoryPath, "settings.json"));
        if (json is "{}")
        {
            Log.Information("Application settings are empty");
            SaveSettings();
            return;
        }
        
        if (!IsValidJson(json))
            return;
        
        Settings = JsonSerializer.Deserialize<Settings>(json) ?? Settings;
        
        var startupState = StartupHelper.IsStartupEnabled();
        if (Settings.StartWithWindows != startupState)
        {
            Log.Information($"Synchronizing registry with StartWithWindows setting: setting={Settings.StartWithWindows}, registry={startupState}");
            if (Settings.StartWithWindows)
                StartupHelper.EnableStartup();
            else StartupHelper.DisableStartup();
        }
        
        Log.Information("Loaded application settings");
    }

    internal static void SaveSettings()
    {
        // Save application settings
        Log.Information("Saving application settings..");
        
        var json = JsonSerializer.Serialize(Settings, JsonSerializerOptions);
        File.WriteAllText(Path.Combine(irHubDirectoryPath, "settings.json"), json);
        
        Log.Information("Saved application settings");
    }
    
    private static bool IsValidJson(string source)
    {
        // Test if given string is valid JSON
        try
        {
            using var doc = JsonDocument.Parse(source);
            Log.Debug($"Successfully validated JSON {source}");
            return true;
        }
        catch (JsonException)
        {
            Log.Debug($"Failed to parse JSON {source}");
            return false;
        }
    }
    
    public static T? DeepCloneT<T>(T obj)
    {
        // Deep clone any type of object
        Log.Information($"Cloning object of type {obj?.GetType()}");
        
        var json = JsonSerializer.Serialize(obj, JsonSerializerOptions);
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
    }
    
    internal static void CopyProperties(Program source, Program destination)
    {
        // Use reflection to copy over each property
        Log.Information("Copying properties..");
        
        foreach (var property in typeof(Program).GetProperties())
        {
            if (property.CanWrite)
            {
                property.SetValue(destination, property.GetValue(source));
                continue;
            }
            Log.Warning($"Couldn't copy property {property.Name} of type {property.PropertyType.Name}");
        }
    }
    
    internal static async Task CheckProgramsRunning()
    {
        // Check all programs if they're running
        Log.Information("Checking if programs are running..");
        
        var processes = Process.GetProcesses();

        foreach (var program in Programs)
        {
            if (program.State is ProgramState.NotFound && File.Exists(program.FilePath))
                await program.ChangeState(ProgramState.Stopped);
            if (program.State is ProgramState.NotFound && !File.Exists(program.FilePath))
                continue;
            if (program.State is ProgramState.Running && program.Process is not null && !program.Process.HasExited)
                continue;

            try
            {
                var existingProcess = processes.FirstOrDefault(process => process.ProcessName == program.ExecutableName);
                if (existingProcess is null || existingProcess.HasExited)
                {
                    if (program.State is not ProgramState.Stopped)
                        Log.Information($"Process {program.ExecutableName} has exited early - CheckProgramsRunning");
                    continue;
                }
                
                program.Process = existingProcess;
                if (program.ExecutableName != existingProcess.ProcessName)
                    program.ExecutableName = existingProcess.ProcessName;

                await program.ChangeState(ProgramState.Running);
                AddProcessEventHandlers(program, program.Process);
            }
            catch (InvalidOperationException)
            {
                Log.Debug($"Process object is no longer valid when checking {program.ExecutableName}");
            }
            catch (Exception ex)
            {
                Log.Warning($"Unexpected error checking process for {program.ExecutableName}: {ex.Message}");
            }
        }
    }

    internal static bool IsProgramRunning(Program program)
    {
        // Check if program is running
        Log.Information($"Checking if {program.Name} is running..");
        
        try
        {
            var existingProcess = Process.GetProcesses().FirstOrDefault(process => process.ProcessName == program.ExecutableName);
            if (existingProcess is null || existingProcess.HasExited)
            {
                Log.Information($"Process {program.Name} is not running.");
                return false;
            }
            
            program.Process = existingProcess;
            if (program.ExecutableName != existingProcess.ProcessName)
                program.ExecutableName = existingProcess.ProcessName;

            AddProcessEventHandlers(program, program.Process);
            return true;
        }
        catch (InvalidOperationException)
        {
            Log.Warning($"Process {program.Name} became invalid while setting up - likely exited during check");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error($"Unexpected error setting up process {program.Name}: {ex.Message}");
            return false;
        }
    }

    private static void AddProcessEventHandlers(Program program, Process? process)
    {
        // Add event handlers like the process exiting
        if (process is null || process.HasExited) return;
        
        Log.Information($"Adding event handlers for {program.ExecutableName} on process {process.Id}");

        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += async (_, _) =>
            {
                Log.Information($"Program {program.ExecutableName} has exited - checking for ancestor processes");
              
                await Task.Delay(1000);
                var processes = Process.GetProcessesByName(program.ExecutableName);
                if (processes.Length is not 0)
                    return;
              
                if (program.State != ProgramState.Stopped)
                    await program.ChangeState(ProgramState.Stopped);
            };
        }
        catch (InvalidOperationException)
        {
            Log.Warning($"Process {program.Name} became invalid while adding exit event handler - likely exited during check");
        }
        catch (Exception ex)
        {
            Log.Error($"Unexpected error adding exit event handler {program.Name}: {ex.Message}");
        }
    }
    
    internal static async Task<bool> StartProgram(Program program)
    {
        Log.Information($"Starting {program.Name}..");
        
        // Check if the program is already running
        if (IsProgramRunning(program))
        {
            Log.Information($"{program.Name} is already running");
            await program.ChangeState(ProgramState.Running);
            return true;
        }

        var startInfo = GetApplicationStartInfo(program);
        if (startInfo is null)
        {
            Log.Information($"Could not get start info for {program.Name}");
            await program.ChangeState(ProgramState.NotFound);
            return false;
        }
        
        Log.Information($"Starting process for {program.Name}..");

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception)
        {
            Log.Information($"Could not start process for {program.Name}. Likely requires administrator privileges. Attempting to start with administrator privileges..");
            
            startInfo = GetApplicationStartInfo(program, true);
            if (startInfo is null)
            {
                Log.Information($"Could not get start info for {program.Name}");
                await program.ChangeState(ProgramState.NotFound);
                return false;
            }
            
            Log.Information($"Starting admin process for {program.Name}..");
            process = Process.Start(startInfo);
        }
      
        if (program.MinimizeToTray)
        {
            await Task.Delay(program.MinimizeToTrayDelay);
            
            var minimized = ApplicationWindowHelper.MinimizeWindowInterop(process);
        }
        else if (program.CloseToTray)
        {
            await Task.Delay(program.CloseToTrayDelay);
            
            var closed = ApplicationWindowHelper.CloseWindow(process);
            if (!closed)
            {
                closed = ApplicationWindowHelper.CloseWindowInterop(process);
            }
        }
        
        await Task.Delay(200);
        
        if (process is null || process.HasExited)
        {
            var processes = Process.GetProcessesByName(program.ExecutableName);
            if (processes.Length < 1)
            {
                Log.Warning($"Failed to start process for {program.Name}");
                Growl.Warning($"Failed to start {program.Name}");
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
        
        Log.Information($"Successfully started {program.Name}");
        return true;
    }
    
    internal static async Task StopProgram(Program program)
    {
        Log.Information($"Stopping {program.Name}..");
        if (program.Process is null || program.Process.HasExited)
        {
            if (program.FilePath is "")
            {
                Log.Warning("Program has no FilePath set - StopProgram");
                return;
            }
            
            KillProcessesByPartialName(program.ExecutableName);
            return;
        }
        
        Log.Information($"Disassociating process for {program.Name}..");
        string processName;
        try
        {
            processName = program.Process.ProcessName;
            program.Process.Close();
        }
        catch (InvalidOperationException)
        {
            Log.Warning($"Process for {program.Name} became invalid while stopping - using ExecutableName as fallback");
            processName = program.ExecutableName;
        }
        catch (Exception ex)
        {
            Log.Warning($"Error getting process name for {program.Name} while stopping: {ex.Message} - using ExecutableName as fallback");
            processName = program.ExecutableName;
        }

        KillProcessesByPartialName(processName);

        const string g61 = "garage61";
        if (processName.Contains(g61, StringComparison.InvariantCultureIgnoreCase))
            KillProcessesByPartialName(g61);

        program.Process = null;
        await program.ChangeState(ProgramState.Stopped);
    }

    private static ProcessStartInfo? GetApplicationStartInfo(Program program, bool requiresAdmin = false)
    {
        Log.Information($"Getting start info for {program.Name}");
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
                Log.Information($"Setting {program.Name} to start hidden");
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
        }

        if (requiresAdmin)
        {
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;
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
        Log.Information("Starting custom post-application-start logic..");

        try
        {
            if (process.HasExited)
            {
                Log.Information("Process has already exited - skipping post-application-start logic");
                return;
            }

            const string onesim = "1simracing";
            if (process.ProcessName.Contains(onesim, StringComparison.InvariantCultureIgnoreCase))
            {
                var hWnd = IntPtr.Zero;
                var retries = 0;
                
                while ((hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) && retries <= MaxRetries)
                {
                    hWnd = FindWindow(null, process.ProcessName);
                    await Task.Delay(200);

                    retries++;
                }

                if (hWnd == IntPtr.Zero)
                {
                    Log.Warning("Could not find window for 1simracing");
                    return;
                }
                
                Log.Information($"1simracing window found at {hWnd}");
                ShowWindow(hWnd, SW_MINIMIZE);
                ShowWindow(hWnd, SW_HIDE);
            }
        }
        catch (InvalidOperationException)
        {
            Log.Warning("Process became invalid during post-application-start logic");
        }
        catch (Exception ex)
        {
            Log.Error($"Error in post-application-start logic: {ex.Message}");
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
    
    #region Processes
    internal static List<Process> GetProcessesByPartialName(string name)
    {
        Log.Information($"Attempting to retrieve processes with partial name: {name}");
        
        try
        {
            var processes = Process.GetProcesses()
                .Where(x => x.ProcessName.Contains(name, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            foreach (var process in processes)
                Log.Information($"Found process {process.Id} with name {process.ProcessName}");
            return processes;
        }
        catch (InvalidOperationException)
        {
            Log.Debug($"Process object is no longer valid when checking for partial name: {name}");
        }
        catch (Exception ex)
        {
            Log.Error($"Error while retrieving processes with partial name {name}: {ex.Message}");
        }
        
        return [];
    }

    internal static void KillProcessesByPartialName(string name)
    {
        Log.Information($"Attempting to kill processes with partial name: {name}..");
        var processes = GetProcessesByPartialName(name);
        
        foreach (var process in processes)
        {
            try
            {
                Log.Information($"Killing process: {process.ProcessName}, process id: {process.Id}");
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Process is no longer associated with a running process
                Log.Debug($"Process object is no longer valid when trying to kill processes with partial name: {name}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error while killing process with partial name {name}: {ex.Message}");
            }
        }
    }
    #endregion
    
    #region Parallelization
    internal static async Task<(int success, int failed)> StartProgramsParallel(IEnumerable<Program> programs, int maxConcurrency = -1)
    {
        var programList = programs.ToList();
        if (programList.Count is 0) return (0, 0);

        if (maxConcurrency <= 0)
            maxConcurrency = Math.Max(2, Environment.ProcessorCount / 2);

        var successCount = 0;
        var failedCount = 0;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency
        };

        await Parallel.ForEachAsync(programList, parallelOptions, async (program, _) =>
        {
            try
            {
                var success = await StartProgram(program);
                if (success)
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failedCount);
            }
            catch
            {
                Interlocked.Increment(ref failedCount);
            }
        });

        return (successCount, failedCount);
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