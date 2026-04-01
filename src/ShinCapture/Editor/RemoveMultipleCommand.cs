using System.Collections.Generic;
using System.Linq;

namespace ShinCapture.Editor;

public class RemoveMultipleCommand : IEditorCommand
{
    private readonly List<EditorObject> _objects;
    private readonly List<(EditorObject obj, int index)> _removed = new();

    public RemoveMultipleCommand(List<EditorObject> objects, IEnumerable<EditorObject> targets)
    {
        _objects = objects;
        foreach (var t in targets)
            _removed.Add((t, objects.IndexOf(t)));
    }

    public void Execute()
    {
        foreach (var (obj, _) in _removed)
            _objects.Remove(obj);
    }

    public void Undo()
    {
        foreach (var (obj, index) in _removed.OrderBy(x => x.index))
        {
            if (index >= 0 && index <= _objects.Count)
                _objects.Insert(index, obj);
            else
                _objects.Add(obj);
        }
    }
}
