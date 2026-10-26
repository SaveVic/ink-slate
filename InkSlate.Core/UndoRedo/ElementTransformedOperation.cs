using System.Windows;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.UndoRedo;

public sealed class ElementTransformedOperation : IUndoableOperation
{
    private readonly CanvasElement _element;

    public ElementTransformedOperation(
        Guid pageId,
        CanvasElement element,
        Rect oldBounds,
        Rect newBounds)
    {
        ArgumentNullException.ThrowIfNull(element);

        PageId = pageId;
        _element = element;
        ElementId = element.Id;
        OldBounds = oldBounds;
        NewBounds = newBounds;
    }

    public Guid PageId { get; }
    public Guid ElementId { get; }
    public Rect OldBounds { get; }
    public Rect NewBounds { get; }

    public void Undo() => ApplyBounds(_element, OldBounds);

    public void Redo() => ApplyBounds(_element, NewBounds);

    private static void ApplyBounds(CanvasElement element, Rect bounds)
    {
        element.X = bounds.X;
        element.Y = bounds.Y;
        element.Width = bounds.Width;
        element.Height = bounds.Height;
    }
}

