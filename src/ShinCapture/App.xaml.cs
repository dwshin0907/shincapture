using System.Windows;

namespace ShinCapture;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // TrayManager will be initialized here later
    }
}
