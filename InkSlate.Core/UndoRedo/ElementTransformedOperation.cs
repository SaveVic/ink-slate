using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.UndoRedo;

public sealed record ElementTransformChange(
    CanvasElement Element,
    Rect OldBounds,
    Rect NewBounds,
    Rect? OldCrop = null,
    Rect? NewCrop = null);

public sealed record StrokeTransformChange(
    Stroke TargetStroke,
    StylusPointCollection OldPoints,
    StylusPointCollection NewPoints);

public sealed class ElementTransformedOperation : IUndoableOperation
{
    public ElementTransformedOperation(
        Guid pageId,
        CanvasElement element,
        Rect oldBounds,
        Rect newBounds,
        Rect? oldCrop = null,
        Rect? newCrop = null)
        : this(pageId, [new ElementTransformChange(element, oldBounds, newBounds, oldCrop, newCrop)], null)
    {
    }

    public ElementTransformedOperation(
        Guid pageId,
        IReadOnlyList<ElementTransformChange>? elementChanges,
        IReadOnlyList<StrokeTransformChange>? strokeChanges = null)
    {
        PageId = pageId;
        ElementChanges = elementChanges ?? [];
        StrokeChanges = strokeChanges ?? [];
    }

    public Guid PageId { get; }
    public IReadOnlyList<ElementTransformChange> ElementChanges { get; }
    public IReadOnlyList<StrokeTransformChange> StrokeChanges { get; }

    public Guid ElementId => ElementChanges.Count > 0 ? ElementChanges[0].Element.Id : Guid.Empty;
    public Rect OldBounds => ElementChanges.Count > 0 ? ElementChanges[0].OldBounds : Rect.Empty;
    public Rect NewBounds => ElementChanges.Count > 0 ? ElementChanges[0].NewBounds : Rect.Empty;
    public Rect? OldCrop => ElementChanges.Count > 0 ? ElementChanges[0].OldCrop : null;
    public Rect? NewCrop => ElementChanges.Count > 0 ? ElementChanges[0].NewCrop : null;

    public void Undo()
    {
        foreach (var change in ElementChanges)
        {
            ApplyBounds(change.Element, change.OldBounds);
            if (change.Element is ImageCanvasElement img && (change.OldCrop.HasValue || change.NewCrop.HasValue))
            {
                ApplyCrop(img, change.OldCrop);
            }
        }

        foreach (var change in StrokeChanges)
        {
            change.TargetStroke.StylusPoints = change.OldPoints.Clone();
        }
    }

    public void Redo()
    {
        foreach (var change in ElementChanges)
        {
            ApplyBounds(change.Element, change.NewBounds);
            if (change.Element is ImageCanvasElement img && (change.OldCrop.HasValue || change.NewCrop.HasValue))
            {
                ApplyCrop(img, change.NewCrop);
            }
        }

        foreach (var change in StrokeChanges)
        {
            change.TargetStroke.StylusPoints = change.NewPoints.Clone();
        }
    }

    private static void ApplyBounds(CanvasElement element, Rect bounds)
    {
        element.X = bounds.X;
        element.Y = bounds.Y;
        element.Width = bounds.Width;
        element.Height = bounds.Height;
    }

    private static void ApplyCrop(ImageCanvasElement img, Rect? crop)
    {
        if (crop.HasValue && !crop.Value.IsEmpty)
        {
            img.CropX = crop.Value.X;
            img.CropY = crop.Value.Y;
            img.CropWidth = crop.Value.Width;
            img.CropHeight = crop.Value.Height;
        }
        else
        {
            img.CropX = 0;
            img.CropY = 0;
            img.CropWidth = 0;
            img.CropHeight = 0;
        }
    }
}
