using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ShinCapture.Models;

namespace ShinCapture.Services;

public class SaveManager
{
    public void SaveToFile(Bitmap bitmap, string filePath, string format, int jpgQuality)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        if (format.ToLower() is "jpg" or "jpeg")
        {
            var encoder = GetEncoder("image/jpeg");
            var qualityParam = new EncoderParameter(Encoder.Quality, jpgQuality);
            var encoderParams = new EncoderParameters(1) { Param = { [0] = qualityParam } };
            bitmap.Save(filePath, encoder, encoderParams);
        }
        else
        {
            bitmap.Save(filePath, GetImageFormat(format));
        }
    }

    public string SaveAuto(Bitmap bitmap, AppSettings settings)
    {
        var dir = settings.Save.AutoSavePath;
        Directory.CreateDirectory(dir);
        var fileName = GenerateFileName(settings.Save);
        var ext = settings.Save.DefaultFormat;
        var filePath = Path.Combine(dir, $"{fileName}.{ext}");
        SaveToFile(bitmap, filePath, ext, settings.Save.JpgQuality);
        return filePath;
    }

    public string? SaveAs(Bitmap bitmap)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|BMP (*.bmp)|*.bmp|GIF (*.gif)|*.gif",
            DefaultExt = ".png",
            FileName = GenerateFileName(new SaveSettings())
        };
        if (dialog.ShowDialog() == true)
        {
            var ext = Path.GetExtension(dialog.FileName).TrimStart('.');
            SaveToFile(bitmap, dialog.FileName, ext, 90);
            return dialog.FileName;
        }
        return null;
    }

    public static string GenerateFileName(SaveSettings settings)
    {
        var now = DateTime.Now;
        return settings.FileNamePattern
            .Replace("{date}", now.ToString("yyyyMMdd"))
            .Replace("{time}", now.ToString("HHmmss"));
    }

    private static ImageCodecInfo GetEncoder(string mimeType) =>
        ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == mimeType);

    private static ImageFormat GetImageFormat(string format) => format.ToLower() switch
    {
        "jpg" or "jpeg" => ImageFormat.Jpeg,
        "bmp" => ImageFormat.Bmp,
        "gif" => ImageFormat.Gif,
        _ => ImageFormat.Png
    };
}
