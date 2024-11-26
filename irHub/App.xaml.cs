using System.Windows;
using Velopack;

namespace irHub;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        VelopackApp.Build().Run();
    }
}