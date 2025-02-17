using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using irHub.Helpers;
using irHub.Windows;
using Microsoft.Web.WebView2.Core;
using NuGet.Versioning;
using Velopack;

namespace irHub.Dialogs;

public partial class UpdateDialog
{
    public UpdateDialog(SemanticVersion? currentVersion, UpdateInfo newVersion)
    {
        InitializeComponent();
        MoveToMainScreen();

        var releaseNotes = newVersion.TargetFullRelease.NotesHTML;
        string html = ReleaseNotesHelper.GenerateReleaseNotes(releaseNotes);
        string tempFile = Path.Combine(Path.GetTempPath(), "irHubTempReleaseNotes.html");
        File.WriteAllText(tempFile, html);

        Title = $"irHub version {newVersion.TargetFullRelease.Version} is available";
        ReleaseNotesText.Text = $"irHub {newVersion.TargetFullRelease.Version} is now available. Would you like to update now?";
        if (currentVersion is not null)
            ReleaseNotesText.Text = $"irHub {newVersion.TargetFullRelease.Version} is now available. Version {currentVersion} is currently installed. Would you like to update now?";
        WebView.Source = new Uri(tempFile);
    }

    private void MoveToMainScreen()
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Left + (screen.Width - Width) / 2;
        Top = screen.Top + (screen.Height - Height) / 2;
    }

    private void WebView_OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.EndsWith("/Temp/irHubTempReleaseNotes.html")) return;
        
        e.Cancel = true;
        Process.Start("explorer.exe", e.Uri);
    }

    private void Update_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdateDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        Topmost = true;
        Focus();
        Topmost = false;
    }
}