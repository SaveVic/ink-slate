using System.Windows.Ink;

namespace InkSlate.Core.UndoRedo;

public sealed class StrokesChangedOperation : IUndoableOperation
{
    private readonly StrokeCollection _targetStrokes;
    private readonly StrokeCollection _addedStrokes;
    private readonly StrokeCollection _removedStrokes;

    public StrokesChangedOperation(
        Guid pageId,
        StrokeCollection targetStrokes,
        StrokeCollection added,
        StrokeCollection removed)
    {
        ArgumentNullException.ThrowIfNull(targetStrokes);
        ArgumentNullException.ThrowIfNull(added);
        ArgumentNullException.ThrowIfNull(removed);

        PageId = pageId;
        _targetStrokes = targetStrokes;
        _addedStrokes = new StrokeCollection(added);
        _removedStrokes = new StrokeCollection(removed);
    }

    public Guid PageId { get; }

    public void Undo()
    {
        RemovePresentStrokes(_targetStrokes, _addedStrokes);
        AddMissingStrokes(_targetStrokes, _removedStrokes);
    }

    public void Redo()
    {
        RemovePresentStrokes(_targetStrokes, _removedStrokes);
        AddMissingStrokes(_targetStrokes, _addedStrokes);
    }

    private static void RemovePresentStrokes(StrokeCollection target, StrokeCollection toRemove)
    {
        foreach (var stroke in toRemove)
        {
            target.Remove(stroke);
        }
    }

    private static void AddMissingStrokes(StrokeCollection target, StrokeCollection toAdd)
    {
        foreach (var stroke in toAdd)
        {
            if (!target.Contains(stroke))
            {
                target.Add(stroke);
            }
        }
    }
}

