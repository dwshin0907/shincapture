using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class FeatureSettingsTests
{
    [Fact]
    public void PersistsOptionalHwpModeAndWatermarkWithoutChangingOtherHotkeys()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ShinCapture-feature-" + Guid.NewGuid());
        try
        {
            var manager = new SettingsManager(directory);
            var settings = manager.Load();
            Assert.False(settings.Hotkeys.RegionCaptureShiftFirst);
            manager.Save(settings);
            manager.Update(s => s.Hotkeys.RegionCaptureShiftFirst = true);
            manager.Update(s => s.Editor.Watermark = new WatermarkSettings
            {
                Text = "교육자료", FontSize = 48, Opacity = 0.65, Position = 4, White = false
            });
            var loaded = new SettingsManager(directory).Load();
            Assert.True(loaded.Hotkeys.RegionCaptureShiftFirst);
            Assert.Equal("Ctrl+Shift+C", loaded.Hotkeys.RegionCaptureAlt);
            Assert.Equal(settings.Hotkeys.FullscreenCapture, loaded.Hotkeys.FullscreenCapture);
            Assert.Equal("교육자료", loaded.Editor.Watermark.Text);
            Assert.Equal(48, loaded.Editor.Watermark.FontSize);
            Assert.Equal(0.65, loaded.Editor.Watermark.Opacity);
            Assert.Equal(4, loaded.Editor.Watermark.Position);
            Assert.False(loaded.Editor.Watermark.White);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
