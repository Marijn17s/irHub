using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;
using HandyControl.Controls;
using irHub.Classes;
using irHub.Classes.Models;

namespace irHub.Pages;

public partial class SettingsPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void ImportPrograms()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var xmlFile = Path.Combine(documents, "iRacingManager\\settings.xml");

        if (!File.Exists(xmlFile) || !Global.IsFile(xmlFile))
        {
            Growl.Warning("No programs could be imported.");
            return;
        }
        
        var doc = new XmlDocument();
        doc.Load(xmlFile);

        var nodes = doc.SelectNodes("//Programs/Program");
        if (nodes is null)
        {
            Growl.Warning("No programs could be imported.");
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

            if (customIconPath is null || customIconPath is "")
            {
                program.UseExecutableIcon = true;
                program.Icon = Global.GetIconFromFile(program.FilePath);
                Global.Programs.Add(program);
                continue;
            }
            
            program.UseExecutableIcon = false;
            program.Icon = Global.GetIconFromFile(program.IconPath);
            Global.Programs.Add(program);
        }
        
        Global.SavePrograms();
        Global.RefreshPrograms();

        var programsAfter = Global.Programs.Count;
        var newPrograms = programsAfter - programsBefore;
        if (newPrograms > 0)
        {
            Growl.Success($"Successfully imported {newPrograms} programs");
            return;
        }
        Growl.Info("No new programs were imported.");
    }

    private void ImportPrograms_OnClick(object sender, RoutedEventArgs e) => ImportPrograms();
}