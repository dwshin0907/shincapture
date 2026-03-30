using System.Collections.Generic;

namespace ShinCapture.Editor;

public class RemoveObjectCommand : IEditorCommand
{
    private readonly List<EditorObject> _objects;
    private readonly EditorObject _target;
    private int _index;

    public RemoveObjectCommand(List<EditorObject> objects, EditorObject target)
    {
        _objects = objects;
        _target = target;
    }

    public void Execute()
    {
        _index = _objects.IndexOf(_target);
        _objects.Remove(_target);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _objects.Count)
            _objects.Insert(_index, _target);
        else
            _objects.Add(_target);
    }
}
