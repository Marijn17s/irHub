using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using irHub.Helpers;
using Microsoft.Web.WebView2.Core;
using NuGet.Versioning;
using Velopack;

namespace irHub.Dialogs;

public partial class UpdateDialog
{
    public UpdateDialog(SemanticVersion? currentVersion, UpdateInfo newVersion)
    {
        InitializeComponent();

        var t = "# irHub changelogs\n\n## Version 0.9.10\nThis version is a small improvement in terms of performance and has some QoL updates and has a few bugfixes.\n\n## Version 0.9.9\n\nThis version brings a basic auto update system which will later be expanded upon to be more detailed.\n\nIt is recommended to install this new version, so you always stay up to date with the latest features!\n\n## Version 0.9.0\n\nI am glad to announce we finally have a workable version ready for release!\n\nIn this version you'll be able to add your favorite iRacing tools and let them automatically start whenever you start playing iRacing!\n\nNote that there are likely still some bugs; you can report them [here](https://github.com/Marijn17s/irHub/issues/new)\n\nThere is still a lot to be done, and I'm excited to see what we can make in the future.\n\n> SHA256: 492C74A6333C535C1FFE904A12759C7D8A535DBA899523EE3EF4DA87DC03CC41\n\n## Version 0.9.10\nThis version is a small improvement in terms of performance and has some QoL updates and has a few bugfixes.\n\n## Version 0.9.9\n\nThis version brings a basic auto update system which will later be expanded upon to be more detailed.\n\nIt is recommended to install this new version, so you always stay up to date with the latest features!\n\n## Version 0.9.0\n\nI am glad to announce we finally have a workable version ready for release!\n\nIn this version you'll be able to add your favorite iRacing tools and let them automatically start whenever you start playing iRacing!\n\nNote that there are likely still some bugs; you can report them [here](https://github.com/Marijn17s/irHub/issues/new)\n\nThere is still a lot to be done, and I'm excited to see what we can make in the future.\n\n> SHA256: 492C74A6333C535C1FFE904A12759C7D8A535DBA899523EE3EF4DA87DC03CC41";
        var releaseNotes = t;
        //var releaseNotes = newVersion.TargetFullRelease.NotesHTML;
        string html = ReleaseNotesHelper.GenerateReleaseNotes(releaseNotes);
        string tempFile = Path.Combine(Path.GetTempPath(), "irHubTempReleaseNotes.html");
        File.WriteAllText(tempFile, html);

        Title = $"irHub version {newVersion?.TargetFullRelease.Version} is available";
        ReleaseNotesText.Text = $"irHub {newVersion?.TargetFullRelease.Version} is now available. Would you like to update now?";
        if (currentVersion is not null)
            ReleaseNotesText.Text = $"irHub {newVersion?.TargetFullRelease.Version} is now available. Version {currentVersion} is currently installed. Would you like to update now?";
        WebView.Source = new Uri(tempFile);
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