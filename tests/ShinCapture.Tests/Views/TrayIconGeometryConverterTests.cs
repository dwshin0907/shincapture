using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ShinCapture.Editor;
using ShinCapture.Models;
using ShinCapture.Services;
using ShinCapture.Views.Controls;

namespace ShinCapture.Tests.Views;

public class TrayIconGeometryConverterTests
{
    [Fact]
    public void ConvertsEveryCatalogAndUtilityIconToFiniteGeometry()
    {
        TrayIconGeometryConverter converter = new();
        string[] iconKeys =
        [
            .. TrayMenuCatalog.CreateCaptureActions(new HotkeySettings())
                .Select(action => action.IconKey),
            .. EditorToolbarCatalog.Tools.Select(tool => tool.IconKey),
            "editor",
            "folder",
            "settings",
            "key",
            "info",
            "exit",
            "ai",
            "undo",
            "redo",
            "copy",
            "save-as",
            "save",
            "more",
            "history",
            "zoom-out",
            "zoom-in",
            "fit",
            "close"
        ];

        foreach (string iconKey in iconKeys.Distinct())
        {
            Assert.True(TrayIconGeometryConverter.Contains(iconKey), $"Missing icon geometry: {iconKey}");
            Geometry geometry = Assert.IsAssignableFrom<Geometry>(converter.Convert(
                iconKey,
                typeof(Geometry),
                parameter: null!,
                CultureInfo.InvariantCulture));

            Assert.NotSame(Geometry.Empty, geometry);
            AssertFiniteNonEmptyBounds(geometry.Bounds);
        }
    }

    [Fact]
    public void UsesSafeGeometryFallbackForUnknownKey()
    {
        TrayIconGeometryConverter converter = new();

        Geometry geometry = Assert.IsAssignableFrom<Geometry>(converter.Convert(
            "not-a-real-icon",
            typeof(Geometry),
            parameter: null!,
            CultureInfo.InvariantCulture));

        Assert.NotSame(Geometry.Empty, geometry);
        AssertFiniteNonEmptyBounds(geometry.Bounds);
    }

    [Fact]
    public void ConvertBackIsNotSupported()
    {
        TrayIconGeometryConverter converter = new();

        Assert.Throws<NotSupportedException>(() =>
        {
            _ = converter.ConvertBack(
                Geometry.Empty,
                typeof(string),
                parameter: null!,
                CultureInfo.InvariantCulture);
        });
    }

    private static void AssertFiniteNonEmptyBounds(Rect bounds)
    {
        Assert.False(bounds.IsEmpty);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
        Assert.True(double.IsFinite(bounds.X));
        Assert.True(double.IsFinite(bounds.Y));
        Assert.True(double.IsFinite(bounds.Width));
        Assert.True(double.IsFinite(bounds.Height));
    }
}
