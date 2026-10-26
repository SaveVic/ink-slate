using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.UndoRedo;

public sealed class ElementRemovedOperation : IUndoableOperation
{
    private readonly IList<CanvasElement> _targetElements;
    private readonly CanvasElement _element;
    private readonly int _originalIndex;

    public ElementRemovedOperation(
        Guid pageId,
        IList<CanvasElement> targetElements,
        CanvasElement element,
        int originalIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(targetElements);
        ArgumentNullException.ThrowIfNull(element);

        PageId = pageId;
        _targetElements = targetElements;
        _element = element;
        _originalIndex = originalIndex >= 0 ? originalIndex : targetElements.IndexOf(element);
    }

    public Guid PageId { get; }

    public void Undo()
    {
        if (_targetElements.Contains(_element))
        {
            return;
        }

        var insertIndex = _originalIndex >= 0
            ? Math.Clamp(_originalIndex, 0, _targetElements.Count)
            : _targetElements.Count;

        _targetElements.Insert(insertIndex, _element);
    }

    public void Redo() => _targetElements.Remove(_element);
}

