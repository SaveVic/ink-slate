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
        Rect newBounds,
        Rect? oldCrop = null,
        Rect? newCrop = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        PageId = pageId;
        _element = element;
        ElementId = element.Id;
        OldBounds = oldBounds;
        NewBounds = newBounds;
        OldCrop = oldCrop;
        NewCrop = newCrop;
    }

    public Guid PageId { get; }
    public Guid ElementId { get; }
    public Rect OldBounds { get; }
    public Rect NewBounds { get; }
    public Rect? OldCrop { get; }
    public Rect? NewCrop { get; }

    public void Undo()
    {
        ApplyBounds(_element, OldBounds);
        if (_element is ImageCanvasElement img && (OldCrop.HasValue || NewCrop.HasValue))
        {
            ApplyCrop(img, OldCrop);
        }
    }

    public void Redo()
    {
        ApplyBounds(_element, NewBounds);
        if (_element is ImageCanvasElement img && (OldCrop.HasValue || NewCrop.HasValue))
        {
            ApplyCrop(img, NewCrop);
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

