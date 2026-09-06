using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Tests.Editor;

public class ImageTransformCommandTests
{
    [Theory]
    [InlineData(ImageTransformKind.RotateClockwise, 2, 3, new byte[] { 4, 1, 5, 2, 6, 3 })]
    [InlineData(ImageTransformKind.RotateCounterclockwise, 2, 3, new byte[] { 3, 6, 2, 5, 1, 4 })]
    [InlineData(ImageTransformKind.Rotate180, 3, 2, new byte[] { 6, 5, 4, 3, 2, 1 })]
    [InlineData(ImageTransformKind.FlipHorizontal, 3, 2, new byte[] { 3, 2, 1, 6, 5, 4 })]
    [InlineData(ImageTransformKind.FlipVertical, 3, 2, new byte[] { 4, 5, 6, 1, 2, 3 })]
    public void TransformsExactPixelsAndSupportsUndoRedo(ImageTransformKind kind, int width, int height, byte[] expected)
    {
        RunInSta(() =>
        {
            var source = BitmapSource.Create(3, 2, 96, 96, PixelFormats.Gray8, null,
                new byte[] { 1, 2, 3, 4, 5, 6 }, 3);
            source.Freeze();
            BitmapSource current = source;
            var stack = new CommandStack();
            stack.Execute(new ImageTransformCommand(source, [], kind, image => current = image));
            Assert.Equal(width, current.PixelWidth);
            Assert.Equal(height, current.PixelHeight);
            Assert.True(current.IsFrozen);
            byte[] pixels = new byte[6];
            current.CopyPixels(pixels, width, 0);
            Assert.Equal(expected, pixels);
            var transformed = current;
            stack.Undo();
            Assert.Same(source, current);
            stack.Redo();
            Assert.Same(transformed, current);
        });
    }

    [Fact]
    public void TransformsAnnotationsAndRestoresEditableObjectsOnUndo()
    {
        RunInSta(() =>
        {
            var source = BitmapSource.Create(40, 20, 96, 96, PixelFormats.Bgra32, null, new byte[40 * 20 * 4], 160);
            var shape = new ShapeObject
            {
                Start = new Point(2, 2), End = new Point(12, 12),
                StrokeColor = Colors.Red, FillMode = FillMode.Solid
            };
            List<EditorObject> objects = [shape];
            BitmapSource current = source;
            var command = new ImageTransformCommand(source, objects, ImageTransformKind.FlipHorizontal, image => current = image);
            command.Execute();
            Assert.Empty(objects);
            byte[] pixel = new byte[4];
            current.CopyPixels(new Int32Rect(32, 7, 1, 1), pixel, 4, 0);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixel);
            current.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
            Assert.Equal(0, pixel[3]);
            command.Undo();
            Assert.Same(source, current);
            Assert.Same(shape, Assert.Single(objects));
            command.Execute();
            Assert.Empty(objects);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
