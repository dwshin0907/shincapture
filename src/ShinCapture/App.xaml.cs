using System;
using System.IO;
using System.Threading;
using System.Windows;
using ShinCapture.Services;
using AppMainWindow = ShinCapture.Views.MainWindow;

namespace ShinCapture;

public partial class App : Application
{
    private static Mutex? _mutex;
    private AppMainWindow? _mainWindow;
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ShinCapture_debug.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            Log($"[CRASH] {ex.Exception}");
            ex.Handled = true;
        };

        try
        {
            Log("=== 시작 ===");

            // 중복 실행 방지
            _mutex = new Mutex(true, "ShinCapture_SingleInstance", out bool isNew);
            if (!isNew)
            {
                Log("중복 실행 감지 → 종료");
                MessageBox.Show("신캡쳐가 이미 실행 중입니다.", "신캡쳐", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            Log("Mutex OK");

            var settingsManager = new SettingsManager();
            var saveManager = new SaveManager();
            Log("Services OK");

            _mainWindow = new AppMainWindow(settingsManager, saveManager);
            Log("MainWindow 생성 OK");

            _mainWindow.Show();
            Log("Show() OK");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] {ex}");
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); }
        catch { }
    }
}
