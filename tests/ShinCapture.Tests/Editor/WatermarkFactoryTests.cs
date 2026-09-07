using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor;
using ShinCapture.Models;

namespace ShinCapture.Tests.Editor;

public class WatermarkFactoryTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void FitsLongKoreanTextWithinSmallImageAtEveryPosition(int position) => RunSta(() =>
    {
        var watermark = WatermarkFactory.Create(80, 40, new WatermarkSettings
        {
            Text = "한국AI교육진흥원 워터마크 테스트", FontSize = 200, Position = position
        });
        Assert.True(new Rect(0, 0, 80, 40).Contains(watermark.Bounds));
    });

    [Fact]
    public void ExportIncludesOpacityAndUndoRedoPreservesOtherObjects() => RunSta(() =>
    {
        var source = BitmapSource.Create(200, 100, 96, 96, PixelFormats.Bgra32, null, new byte[80000], 800);
        var watermark = WatermarkFactory.Create(200, 100, new WatermarkSettings { Text = "TEST", Opacity = 0.4 });
        List<EditorObject> objects = [];
        var stack = new CommandStack();
        stack.Execute(new AddObjectCommand(objects, watermark));
        var rendered = EditorCompositeRenderer.RenderBitmapSource(source, objects);
        byte[] pixels = new byte[80000];
        rendered.CopyPixels(pixels, 800, 0);
        var alpha = pixels.Where((_, i) => i % 4 == 3).ToArray();
        Assert.InRange(alpha.Max(), (byte)95, (byte)102);
        Assert.Equal(0, alpha[0]);
        stack.Undo(); Assert.Empty(objects);
        stack.Redo(); Assert.Same(watermark, Assert.Single(objects));
        Assert.Equal(watermark.TextColor, ((ShinCapture.Editor.Objects.TextObject)watermark.Clone()).TextColor);
    });

    [Fact]
    public void EmptyTextIsRejected() => RunSta(() =>
        Assert.Throws<ArgumentException>(() => WatermarkFactory.Create(100, 100, new WatermarkSettings { Text = " " })));

    internal static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
