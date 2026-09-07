using System.Windows;
using ShinCapture.Editor;
using ShinCapture.Models;

namespace ShinCapture.Views;

public partial class EditorWindow
{
    private void OnWatermarkClick(object sender, RoutedEventArgs e)
    {
        SelectTool("선택");
        var preview = EditorCompositeRenderer.RenderBitmapSource(_sourceImage, _objects);
        var dialog = new WatermarkWindow(preview, _settings.Editor.Watermark ?? new WatermarkSettings()) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var settings = dialog.Result;
        _commandStack.Execute(new AddObjectCommand(_objects,
            WatermarkFactory.Create(_sourceImage.PixelWidth, _sourceImage.PixelHeight, settings)));
        _settings.Editor.Watermark = settings;
        SetStatus("워터마크 추가 완료 — 저장·복사에 포함됩니다. Ctrl+Z로 실행취소");
        try { _settingsManager?.Update(s => s.Editor.Watermark = settings, raiseChanged: false); }
        catch { SetStatus("워터마크를 추가했습니다. 다음 실행에 사용할 설정 저장은 실패했습니다."); }
    }
}
