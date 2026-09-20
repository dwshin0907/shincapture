using System;
using System.Collections.Generic;
using System.Linq;

namespace ShinCapture.Editor;

/// <summary>Keeps batch selection independent of the capture open in the editor.</summary>
public sealed class CaptureHistorySelection<T> where T : class
{
    private readonly HashSet<T> _selected = new();
    private T? _anchor;

    public int Count => _selected.Count;
    public bool Contains(T item) => _selected.Contains(item);

    public T[] InDisplayOrder(IEnumerable<T> items) => items.Where(_selected.Contains).ToArray();

    public void SelectOnly(T item)
    {
        _selected.Clear();
        _selected.Add(item);
        _anchor = item;
    }

    public void Toggle(T item)
    {
        if (!_selected.Remove(item)) _selected.Add(item);
        _anchor = item;
    }

    public void SelectRange(IReadOnlyList<T> items, T target, bool additive = false)
    {
        int targetIndex = IndexOf(items, target);
        if (targetIndex < 0) return;
        int anchorIndex = _anchor == null ? -1 : IndexOf(items, _anchor);
        if (anchorIndex < 0)
        {
            anchorIndex = targetIndex;
            _anchor = target;
        }

        if (!additive) _selected.Clear();
        for (int i = Math.Min(anchorIndex, targetIndex); i <= Math.Max(anchorIndex, targetIndex); i++)
            _selected.Add(items[i]);
    }

    public void SelectAll(IEnumerable<T> items)
    {
        _selected.Clear();
        _selected.UnionWith(items);
    }

    public void Clear()
    {
        _selected.Clear();
        _anchor = null;
    }

    public void Retain(IEnumerable<T> items)
    {
        var retained = new HashSet<T>(items);
        _selected.IntersectWith(retained);
        if (_anchor != null && !retained.Contains(_anchor)) _anchor = null;
    }

    public void Remove(T item)
    {
        _selected.Remove(item);
        if (EqualityComparer<T>.Default.Equals(_anchor, item)) _anchor = null;
    }

    public void Replace(T previous, T replacement)
    {
        if (_selected.Remove(previous)) _selected.Add(replacement);
        if (EqualityComparer<T>.Default.Equals(_anchor, previous)) _anchor = replacement;
    }

    private static int IndexOf(IReadOnlyList<T> items, T item)
    {
        for (int i = 0; i < items.Count; i++)
            if (EqualityComparer<T>.Default.Equals(items[i], item)) return i;
        return -1;
    }
}
