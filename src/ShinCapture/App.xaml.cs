using System.Windows;
using ShinCapture.Services;
using AppMainWindow = ShinCapture.Views.MainWindow;

namespace ShinCapture;

public partial class App : Application
{
    private AppMainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settingsManager = new SettingsManager();
        _mainWindow = new AppMainWindow(settingsManager);
    }
}
