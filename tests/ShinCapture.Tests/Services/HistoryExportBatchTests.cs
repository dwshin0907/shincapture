using System.Drawing;
using System.Windows;
using ShinCapture.Services;
using ShinCapture.Tests.Editor;

namespace ShinCapture.Tests.Services;

public sealed class HistoryExportBatchTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"캡처 묶음 테스트 {Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void DragIncludesEveryFileAndOneLineOfQuotedUnicodePaths() => WatermarkFactoryTests.RunSta(() =>
    {
        var exports = new DragExportService(_directory);
        using var bitmap = new Bitmap(8, 6);
        using var batch = new HistoryExportBatch(exports);
        batch.Add(exports.CreatePng(bitmap));
        batch.Add(exports.CreatePng(bitmap));

        DataObject data = batch.CreateDragData();
        Assert.Equal(batch.Paths, data.GetFileDropList().Cast<string>());
        Assert.Equal(string.Join(" ", batch.Paths.Select(path => $"\"{path}\"")), data.GetText(TextDataFormat.UnicodeText));
        Assert.DoesNotContain('\n', batch.PathText);
        Assert.DoesNotContain('\r', batch.PathText);
        Assert.True(data.GetDataPresent("ShinCapture.HistoryExport"));
        Assert.All(batch.Paths, path => Assert.True(File.Exists(path)));
    });

    [Fact]
    public void AllBatchFilesSurviveCachePressureWhileMoreExportsArePrepared()
    {
        var exports = new DragExportService(_directory, maxFiles: 1, maxBytes: 1);
        using var bitmap = new Bitmap(8, 6);
        string[] paths;
        using (var batch = new HistoryExportBatch(exports))
        {
            for (int i = 0; i < 4; i++) batch.Add(exports.CreatePng(bitmap));
            paths = batch.Paths.ToArray();
            exports.Cleanup();
            Assert.All(paths, path => Assert.True(File.Exists(path)));
        }
        exports.Cleanup();
        Assert.All(paths, path => Assert.False(File.Exists(path)));
    }

    [Fact]
    public void SavingCopiesOnlyBatchFilesAndNeverOverwritesAnEarlierSave()
    {
        var exports = new DragExportService(Path.Combine(_directory, "cache"));
        using var first = new Bitmap(8, 6);
        using var second = new Bitmap(12, 10);
        using var unselected = new Bitmap(14, 11);
        using var batch = new HistoryExportBatch(exports);
        batch.Add(exports.CreatePng(first));
        _ = exports.CreatePng(unselected);
        batch.Add(exports.CreatePng(second));
        string destination = Path.Combine(_directory, "선택 저장");

        HistoryBatchSaveResult saved = batch.SaveToDirectory(destination);
        HistoryBatchSaveResult repeated = batch.SaveToDirectory(destination);

        Assert.Equal(0, saved.FailedCount);
        Assert.Equal(2, saved.SavedPaths.Count);
        Assert.Equal(4, Directory.GetFiles(destination).Length);
        Assert.Empty(saved.SavedPaths.Intersect(repeated.SavedPaths));
        Assert.Empty(Directory.GetFiles(destination, "*.tmp"));
        using Image decodedFirst = Image.FromFile(saved.SavedPaths[0]);
        using Image decodedSecond = Image.FromFile(saved.SavedPaths[1]);
        Assert.Equal(8, decodedFirst.Width);
        Assert.Equal(12, decodedSecond.Width);
    }

    [Fact]
    public void PartialSaveReportsFailuresWithoutLeavingBrokenPngsOrPartialFiles()
    {
        var exports = new DragExportService(Path.Combine(_directory, "cache"));
        using var bitmap = new Bitmap(8, 6);
        using var batch = new HistoryExportBatch(exports);
        batch.Add(Path.Combine(_directory, "missing.png"));
        batch.Add(exports.CreatePng(bitmap));
        string destination = Path.Combine(_directory, "saved");

        HistoryBatchSaveResult result = batch.SaveToDirectory(destination);

        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.SavedPaths);
        Assert.Single(Directory.GetFiles(destination));
        using Image decoded = Image.FromFile(result.SavedPaths[0]);
        Assert.Equal(8, decoded.Width);
    }
}
