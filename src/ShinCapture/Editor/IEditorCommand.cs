namespace ShinCapture.Editor;

public interface IEditorCommand
{
    void Execute();
    void Undo();
}
