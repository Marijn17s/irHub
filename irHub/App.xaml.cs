using System.Windows;
using irHub.Classes;
using Velopack;

namespace irHub;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        VelopackApp.Build().Run();

        if (Global.FindProcess() is { } currentProcess)
        {
            Global.FocusProcess(currentProcess);
            Current.Shutdown();
        }
    }
}