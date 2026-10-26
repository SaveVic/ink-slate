using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.UndoRedo;

public sealed record ElementZIndexChange(CanvasElement Element, int OldZIndex, int NewZIndex);

public sealed class ElementZIndexChangedOperation : IUndoableOperation
{
    private readonly IReadOnlyList<ElementZIndexChange> _changes;

    public ElementZIndexChangedOperation(Guid pageId, IReadOnlyList<ElementZIndexChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        PageId = pageId;
        _changes = changes;
    }

    public Guid PageId { get; }

    public IReadOnlyList<ElementZIndexChange> Changes => _changes;

    public void Undo()
    {
        foreach (var change in _changes)
        {
            change.Element.ZIndex = change.OldZIndex;
        }
    }

    public void Redo()
    {
        foreach (var change in _changes)
        {
            change.Element.ZIndex = change.NewZIndex;
        }
    }
}

