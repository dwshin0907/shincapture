using System.Collections.Generic;

namespace ShinCapture.Editor;

public class AddObjectCommand : IEditorCommand
{
    private readonly List<EditorObject> _objects;
    private readonly EditorObject _object;

    public AddObjectCommand(List<EditorObject> objects, EditorObject obj)
    {
        _objects = objects;
        _object = obj;
    }

    public void Execute() => _objects.Add(_object);
    public void Undo() => _objects.Remove(_object);
}
