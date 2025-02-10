using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Models;
using Material.Icons;
using Serilog;

namespace irHub.Pages;

public partial class SettingsPage
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = Global.Settings;
        
        Log.Information("Settings page loaded");
    }

    private void ImportPrograms()
    {
        Log.Information("Importing programs..");
        
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var xmlFile = Path.Combine(documents, "iRacingManager\\settings.xml");

        if (!File.Exists(xmlFile) || !Global.IsFile(xmlFile))
        {
            Log.Warning("No programs could be imported. File does not exist");
            Growl.Warning("No programs could be imported. File does not exist");
            return;
        }
        
        var doc = new XmlDocument();
        doc.Load(xmlFile);

        var nodes = doc.SelectNodes("//Programs/Program");
        if (nodes is null)
        {
            Log.Warning("No programs could be imported. File might be corrupted");
            Growl.Warning("No programs could be imported. File might be corrupted");
            return;
        }

        var programsBefore = Global.Programs.Count;
        
        foreach (XmlNode node in nodes)
        {
            var name = node.SelectSingleNode("DisplayName")?.InnerText;
            bool.TryParse(node.SelectSingleNode("StartWithIRacing")?.InnerText, out var startWithSim);
            bool.TryParse(node.SelectSingleNode("StopWithIRacing")?.InnerText, out var stopWithSim);
            var customIconPath = node.SelectSingleNode("PicturePath")?.InnerText;
            var installationDirectory = node.SelectSingleNode("InstallLocation")?.InnerText;
            var fileName = node.SelectSingleNode("FileName")?.InnerText;
            var installationPath = $"{installationDirectory}\\{fileName}";
            bool.TryParse(node.SelectSingleNode("StartHidden")?.InnerText, out var startHidden);

            var exists = Global.Programs.Any(existingProgram => existingProgram.FilePath == installationPath);
            if (exists) continue;

            name ??= fileName ?? "Unknown name";
            
            var program = new Program
            {
                Name = name,
                FilePath = installationPath,
                StartWithIracingSim = startWithSim,
                StopWithIracingSim = stopWithSim,
                StartHidden = startHidden
            };
            
            if (customIconPath is null or "")
            {
                Log.Warning("Custom icon path is invalid");
                program.SetExecutableIcon();
                Global.Programs.Add(program);
                continue;
            }
            
            program.SetIcon(program.IconPath);
            Global.Programs.Add(program);
        }

        var programsAfter = Global.Programs.Count;
        var newPrograms = programsAfter - programsBefore;
        if (newPrograms > 0)
        {
            Log.Information($"Successfully imported {newPrograms} programs");
            
            Global.SavePrograms();
            Global.RefreshPrograms();
            
            Growl.Success($"Successfully imported {newPrograms} programs");
            return;
        }
        Log.Information("No new programs were imported.");
        Growl.Info("No new programs were imported.");
    }

    private void ImportPrograms_OnClick(object sender, RoutedEventArgs e) => ImportPrograms();

    private async void CopyGarageCoverUrl_OnClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Global.Settings.GarageCoverUrl);
        CopyButtonIcon.Kind = MaterialIconKind.Check;
        
        await Task.Delay(1500);
        CopyButtonIcon.Kind = MaterialIconKind.ContentCopy;
    }
}