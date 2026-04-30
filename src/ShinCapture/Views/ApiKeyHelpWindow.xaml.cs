using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class ApiKeyHelpWindow : Window
{
    private const string GuideUrl1 = "https://contents.premium.naver.com/market/ai/contents/250705112417030po";
    private const string GuideUrl2 = "https://contents.premium.naver.com/market/ai/contents/250705163212545ak";

    private readonly SettingsManager? _settingsManager;

    public ApiKeyHelpWindow(SettingsManager? settingsManager = null)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void OnGuideCard1Click(object sender, MouseButtonEventArgs e) => OpenUrl(GuideUrl1);
    private void OnGuideCard2Click(object sender, MouseButtonEventArgs e) => OpenUrl(GuideUrl2);

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsManager == null) return;
        // AI 탭 인덱스: 일반=0, 캡쳐=1, 저장=2, 단축키=3, 지정사이즈=4, AI=5
        var win = new SettingsWindow(_settingsManager, initialTabIndex: 5);
        win.Owner = this;
        win.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // 무시
        }
    }
}
