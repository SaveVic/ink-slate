using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Shapes;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;

namespace InkSlate.App.Controls;

public partial class PageCanvasControl
{
    public Ellipse EraserCursor => EraserCursorIndicator;

    public void RefreshEraserShape()
    {
        var clampedSize = double.IsFinite(EraserSize) && EraserSize > 0.0
            ? Math.Clamp(EraserSize, 2.0, 128.0)
            : ToolbarViewModel.DefaultEraserSize;

        InternalInkCanvas.EraserShape = new EllipseStylusShape(clampedSize, clampedSize);

        if (InternalInkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            InternalInkCanvas.EditingMode = InkCanvasEditingMode.None;
            InternalInkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        }

        if (EraserCursorIndicator is not null)
        {
            EraserCursorIndicator.Width = clampedSize;
            EraserCursorIndicator.Height = clampedSize;
        }
    }

    public void UpdateEraserCursor(Point point)
    {
        if (ActiveToolMode != ToolMode.Eraser || ActiveEraserMode != EraserMode.Point)
        {
            HideEraserCursor();
            return;
        }

        var clampedSize = double.IsFinite(EraserSize) && EraserSize > 0.0
            ? Math.Clamp(EraserSize, 2.0, 128.0)
            : ToolbarViewModel.DefaultEraserSize;

        if (EraserCursorIndicator is not null)
        {
            EraserCursorIndicator.Width = clampedSize;
            EraserCursorIndicator.Height = clampedSize;
            Canvas.SetLeft(EraserCursorIndicator, point.X - (clampedSize / 2.0));
            Canvas.SetTop(EraserCursorIndicator, point.Y - (clampedSize / 2.0));
            EraserCursorIndicator.Visibility = Visibility.Visible;
        }
    }

    public void HideEraserCursor()
    {
        if (EraserCursorIndicator is not null)
        {
            EraserCursorIndicator.Visibility = Visibility.Collapsed;
        }
    }

    public int EraseElementsAtPoint(Point pagePoint)
    {
        if (PageElements is not { Count: > 0 })
        {
            return 0;
        }

        var hitElements = new List<CanvasElementViewModel>();
        for (var i = PageElements.Count - 1; i >= 0; i--)
        {
            var element = PageElements[i];
            if (element.HitTest(pagePoint))
            {
                hitElements.Add(element);
            }
        }

        if (hitElements.Count == 0)
        {
            return 0;
        }

        var removedCount = 0;
        foreach (var element in hitElements)
        {
            if (RemoveElementCommand is not null && RemoveElementCommand.CanExecute(element))
            {
                RemoveElementCommand.Execute(element);
                removedCount++;
            }
            else
            {
                PageElements.Remove(element);
                removedCount++;
            }
        }

        return removedCount;
    }

    public bool DeleteSelection() => DeleteSelected();

    public bool DeleteSelected()
    {
        var deleted = false;
        if (DataContext is PageViewModel pageVm)
        {
            var selected = pageVm.Elements.Where(e => e.IsSelected && !e.IsEditing).ToList();
            if (selected.Count > 0)
            {
                foreach (var el in selected)
                {
                    pageVm.RemoveElement(el);
                }
                deleted = true;
            }
        }
        else if (PageElements is not null)
        {
            var selected = PageElements.Where(e => e.IsSelected && !e.IsEditing).ToList();
            if (selected.Count > 0)
            {
                foreach (var el in selected)
                {
                    if (RemoveElementCommand is not null && RemoveElementCommand.CanExecute(el))
                    {
                        RemoveElementCommand.Execute(el);
                    }
                    else
                    {
                        PageElements.Remove(el);
                    }
                }
                deleted = true;
            }
        }

        if (DeleteSelectedStrokes())
        {
            deleted = true;
        }

        if (deleted)
        {
            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
        }

        return deleted;
    }

    public bool DeleteSelectedStrokes()
    {
        var strokes = InternalInkCanvas.GetSelectedStrokes();
        if (strokes.Count > 0)
        {
            var strokesToRemove = new StrokeCollection(strokes);
            InternalInkCanvas.Strokes.Remove(strokesToRemove);

            _isSyncingSelection = true;
            try
            {
                InternalInkCanvas.Select(new StrokeCollection(), []);
                SelectedStrokes = null;
                if (DataContext is PageViewModel pageVm)
                {
                    pageVm.SelectedStrokes = null;
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }

            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
            return true;
        }
        return false;
    }
}
