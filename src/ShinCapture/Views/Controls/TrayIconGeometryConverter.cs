using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ShinCapture.Views.Controls;

public sealed class TrayIconGeometryConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string, Geometry> Icons =
        new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase)
        {
            ["region"] = Create("M3,8 L3,3 L8,3 M16,3 L21,3 L21,8 M21,16 L21,21 L16,21 M8,21 L3,21 L3,16 M8,8 L16,8 L16,16 L8,16 Z"),
            ["window"] = Create("M3,5 L21,5 L21,19 L3,19 Z M3,9 L21,9"),
            ["fullscreen"] = Create("M3,4 L21,4 L21,18 L3,18 Z M8,21 L16,21 M12,18 L12,21"),
            ["scroll"] = Create("M5,3 L19,3 L19,21 L5,21 Z M8,7 L16,7 M8,11 L16,11 M12,14 L12,19 M9,16 L12,19 L15,16"),
            ["spark"] = Create("M12,2 L14,9 L21,12 L14,15 L12,22 L10,15 L3,12 L10,9 Z"),
            ["fixed"] = Create("M4,6 L20,6 L20,18 L4,18 Z M7,3 L7,9 M17,3 L17,9 M1,12 L7,12 M17,12 L23,12"),
            ["freeform"] = Create("M4,18 C6,5 11,4 13,11 C15,18 19,18 21,7 M4,18 L8,20"),
            ["element"] = Create("M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z"),
            ["text"] = Create("M4,5 L20,5 M12,5 L12,20 M7,20 L17,20"),
            ["translate"] = Create("M3,4 L13,4 L13,13 L8,13 L4,17 L4,13 L3,13 Z M11,10 L21,10 L21,19 L17,19 L14,22 L14,19 L11,19 Z M6,8 L10,8 M8,6 L8,11 M15,14 L18,18 M18,14 L15,18"),
            ["cursor"] = Create("M5,2 L5,19 L9,15 L12,22 L15,20 L12,14 L18,14 Z"),
            ["pen"] = Create("M4,20 L8,19 L20,7 L17,4 L5,16 Z M15,6 L18,9 M4,20 L5,16"),
            ["highlighter"] = Create("M5,17 L14,8 L18,12 L9,21 L5,21 Z M12,6 L15,3 L21,9 L18,12 M3,21 L12,21"),
            ["shape"] = Create("M3,4 L13,4 L13,14 L3,14 Z M16,10 A5,5 0 1 0 16,20 A5,5 0 1 0 16,10"),
            ["arrow"] = Create("M4,20 L20,4 M12,4 L20,4 L20,12"),
            ["balloon"] = Create("M4,4 L20,4 L20,16 L11,16 L6,21 L6,16 L4,16 Z"),
            ["mosaic"] = Create("M3,3 L9,3 L9,9 L3,9 Z M10,3 L16,3 L16,9 L10,9 Z M17,3 L21,3 L21,9 L17,9 Z M3,10 L9,10 L9,16 L3,16 Z M10,10 L16,10 L16,16 L10,16 Z M17,10 L21,10 L21,16 L17,16 Z M3,17 L9,17 L9,21 L3,21 Z M10,17 L16,17 L16,21 L10,21 Z M17,17 L21,17 L21,21 L17,21 Z"),
            ["blur"] = Create("M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,7 A5,5 0 1 0 12,17 A5,5 0 1 0 12,7 M12,11 A1,1 0 1 0 12,13 A1,1 0 1 0 12,11"),
            ["number"] = Create("M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M10,9 L12,7 L12,17 M9,17 L15,17"),
            ["image"] = Create("M3,4 L21,4 L21,20 L3,20 Z M6,15 L10,11 L13,14 L16,10 L21,16 M8,8 A1,1 0 1 0 8,10 A1,1 0 1 0 8,8"),
            ["eyedropper"] = Create("M14,4 L20,10 M12,6 L18,12 M5,19 L12,12 M4,20 L8,19 L19,8 L16,5 Z"),
            ["crop"] = Create("M7,3 L7,17 L21,17 M3,7 L17,7 L17,21"),
            ["eraser"] = Create("M5,17 L14,8 L20,14 L12,22 L6,22 L2,18 Z M10,12 L16,18 M12,22 L22,22"),
            ["ai"] = Create("M12,2 L14,9 L21,12 L14,15 L12,22 L10,15 L3,12 L10,9 Z M19,3 L19.7,5.3 L22,6 L19.7,6.7 L19,9 L18.3,6.7 L16,6 L18.3,5.3 Z"),
            ["undo"] = Create("M9,7 L4,12 L9,17 M5,12 L14,12 C18,12 20,15 20,19"),
            ["redo"] = Create("M15,7 L20,12 L15,17 M19,12 L10,12 C6,12 4,15 4,19"),
            ["copy"] = Create("M8,8 L21,8 L21,21 L8,21 Z M3,3 L16,3 L16,8 M3,3 L3,16 L8,16"),
            ["save-as"] = Create("M4,3 L17,3 L21,7 L21,21 L4,21 Z M8,3 L8,9 L16,9 L16,3 M8,21 L8,14 L17,14 L17,21 M15,12 L22,5 M18,5 L22,5 L22,9"),
            ["save"] = Create("M4,3 L18,3 L21,6 L21,21 L4,21 Z M8,3 L8,9 L16,9 L16,3 M8,21 L8,14 L17,14 L17,21"),
            ["more"] = Create("M5,11 A1,1 0 1 0 5,13 A1,1 0 1 0 5,11 M12,11 A1,1 0 1 0 12,13 A1,1 0 1 0 12,11 M19,11 A1,1 0 1 0 19,13 A1,1 0 1 0 19,11"),
            ["history"] = Create("M4,5 L20,5 L20,20 L4,20 Z M8,2 L8,8 M16,2 L16,8 M4,10 L20,10 M8,14 L16,14 M8,17 L14,17"),
            ["zoom-out"] = Create("M10,4 A6,6 0 1 0 10,16 A6,6 0 1 0 10,4 M14,14 L21,21 M7,10 L13,10"),
            ["zoom-in"] = Create("M10,4 A6,6 0 1 0 10,16 A6,6 0 1 0 10,4 M14,14 L21,21 M7,10 L13,10 M10,7 L10,13"),
            ["fit"] = Create("M3,9 L3,3 L9,3 M15,3 L21,3 L21,9 M21,15 L21,21 L15,21 M9,21 L3,21 L3,15"),
            ["close"] = Create("M5,5 L19,19 M19,5 L5,19"),
            ["editor"] = Create("M4,20 L8,19 L19,8 L16,5 L5,16 Z M14,7 L17,10 M4,20 L5,16"),
            ["folder"] = Create("M3,6 L10,6 L12,9 L21,9 L21,19 L3,19 Z M3,6 L3,19"),
            ["settings"] = Create("M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M12,2 L12,5 M12,19 L12,22 M2,12 L5,12 M19,12 L22,12 M5,5 L7,7 M17,17 L19,19 M19,5 L17,7 M7,17 L5,19"),
            ["key"] = Create("M4,15 C4,12 6,10 9,10 C12,10 14,12 14,15 C14,18 12,20 9,20 C6,20 4,18 4,15 Z M13,12 L21,4 M17,8 L20,11 M19,6 L22,9"),
            ["info"] = Create("M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,10 L12,17 M12,7 L12.01,7"),
            ["exit"] = Create("M4,3 L15,3 L15,8 M15,16 L15,21 L4,21 Z M10,12 L22,12 M18,8 L22,12 L18,16")
        };

    private static readonly Geometry Fallback = Icons["region"];

    public static bool Contains(string key) => Icons.ContainsKey(key);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string key && Icons.TryGetValue(key, out Geometry? geometry)
            ? geometry
            : Fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Geometry Create(string pathData)
    {
        Geometry geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
