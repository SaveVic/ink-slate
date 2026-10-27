using System.Windows.Ink;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.UndoRedo;

public sealed class PasteObjectsOperation : IUndoableOperation
{
    private readonly IList<CanvasElement> _targetElements;
    private readonly IReadOnlyList<CanvasElement> _pastedElements;
    private readonly StrokeCollection _targetStrokes;
    private readonly StrokeCollection _pastedStrokes;

    public PasteObjectsOperation(
        Guid pageId,
        IList<CanvasElement> targetElements,
        IReadOnlyList<CanvasElement> pastedElements,
        StrokeCollection targetStrokes,
        StrokeCollection pastedStrokes)
    {
        ArgumentNullException.ThrowIfNull(targetElements);
        ArgumentNullException.ThrowIfNull(pastedElements);
        ArgumentNullException.ThrowIfNull(targetStrokes);
        ArgumentNullException.ThrowIfNull(pastedStrokes);

        PageId = pageId;
        _targetElements = targetElements;
        _pastedElements = pastedElements;
        _targetStrokes = targetStrokes;
        _pastedStrokes = new StrokeCollection(pastedStrokes);
    }

    public Guid PageId { get; }

    public void Undo()
    {
        foreach (var element in _pastedElements)
        {
            _targetElements.Remove(element);
        }

        foreach (var stroke in _pastedStrokes)
        {
            _targetStrokes.Remove(stroke);
        }
    }

    public void Redo()
    {
        foreach (var element in _pastedElements)
        {
            if (!_targetElements.Contains(element))
            {
                _targetElements.Add(element);
            }
        }

        foreach (var stroke in _pastedStrokes)
        {
            if (!_targetStrokes.Contains(stroke))
            {
                _targetStrokes.Add(stroke);
            }
        }
    }
}

