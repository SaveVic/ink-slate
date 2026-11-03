using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;

namespace InkSlate.App.Controls;

public partial class PageCanvasControl
{
    private const double DefaultStrokeHitToleranceDip = 4.0;

    private bool _isPointerDraggingObjects;
    private bool _isPointerRectSelecting;
    private Point _pointerDragStartPoint;
    private bool _hasPointerDraggedBeyondThreshold;
    private CanvasElementViewModel? _pendingSingleSelectElement;
    private Stroke? _pendingSingleSelectStroke;

    public Stroke? HitTestTopmostStroke(Point canvasPoint, double toleranceDip = DefaultStrokeHitToleranceDip)
    {
        var strokes = InternalInkCanvas.Strokes;
        if (strokes is null || strokes.Count == 0)
        {
            return null;
        }

        for (var i = strokes.Count - 1; i >= 0; i--)
        {
            var stroke = strokes[i];
            if (stroke.HitTest(canvasPoint, toleranceDip))
            {
                return stroke;
            }
        }

        return null;
    }

    public CanvasElementViewModel? HitTestTopmostElement(Point canvasPoint)
    {
        if (PageElements is null || PageElements.Count == 0)
        {
            return null;
        }

        // Select the hit element with highest ZIndex; break ties by highest index (most recently added)
        CanvasElementViewModel? bestMatch = null;
        for (var i = PageElements.Count - 1; i >= 0; i--)
        {
            var el = PageElements[i];
            if (el.HitTest(canvasPoint))
            {
                if (bestMatch is null || el.ZIndex > bestMatch.ZIndex)
                {
                    bestMatch = el;
                }
            }
        }

        return bestMatch;
    }

    public void SelectSingleObject(CanvasElementViewModel? element, Stroke? stroke)
    {
        _isSyncingSelection = true;
        try
        {
            if (DataContext is PageViewModel pageVm)
            {
                pageVm.SelectSingleElement(element);
                if (element is not null)
                {
                    element.IsSelected = true;
                }
                pageVm.SelectedStrokes = stroke is not null ? [stroke] : null;
            }
            else if (PageElements is not null)
            {
                foreach (var el in PageElements)
                {
                    el.IsSelected = ReferenceEquals(el, element);
                    if (!el.IsSelected)
                    {
                        el.IsEditing = false;
                    }
                }
            }

            var strokes = stroke is not null ? new StrokeCollection([stroke]) : new StrokeCollection();
            var elements = new List<UIElement>();
            if (element is not null && _containers.TryGetValue(element, out var container))
            {
                elements.Add(container);
            }

            SelectedStrokes = strokes;
            InternalInkCanvas.Select(strokes, elements);
            foreach (var c in _containers.Values)
            {
                c.RefreshAdornerAttachment();
            }
            SnapshotSelectionState();
            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    public bool IsPointInsideSelectionBox(Point canvasPoint)
    {
        if (ActiveToolMode != ToolMode.Pointer)
        {
            return false;
        }

        // Check selected strokes
        var selectedStrokes = InternalInkCanvas.GetSelectedStrokes();
        if (selectedStrokes is not null && selectedStrokes.Count > 0)
        {
            var bounds = selectedStrokes.GetBounds();
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
            {
                const double padding = 4.0;
                var strokeBox = new Rect(
                    Math.Max(0.0, bounds.Left - padding),
                    Math.Max(0.0, bounds.Top - padding),
                    bounds.Width + (padding * 2.0),
                    bounds.Height + (padding * 2.0));

                if (strokeBox.Contains(canvasPoint))
                {
                    return true;
                }
            }
        }

        // Check selected elements
        if (PageElements is not null)
        {
            foreach (var el in PageElements)
            {
                if (el.IsSelected)
                {
                    var elRect = new Rect(el.X, el.Y, Math.Max(0.0, el.Width), Math.Max(0.0, el.Height));
                    if (elRect.Contains(canvasPoint))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void UpdatePointerCursor(Point canvasPoint)
    {
        if (ActiveToolMode != ToolMode.Pointer)
        {
            return;
        }

        if (_isPointerDraggingObjects)
        {
            Cursor = Cursors.SizeAll;
        }
        else if (_isPointerRectSelecting)
        {
            Cursor = Cursors.Arrow;
        }
        else if (IsPointInsideSelectionBox(canvasPoint))
        {
            Cursor = Cursors.SizeAll;
        }
        else
        {
            Cursor = Cursors.Arrow;
        }
    }

    protected override void OnQueryCursor(QueryCursorEventArgs e)
    {
        base.OnQueryCursor(e);

        if (FindAncestor<ZoomScrollViewer>(this) is { } scrollViewer &&
            (scrollViewer.IsSpaceKeyDown || scrollViewer.IsPanning))
        {
            return;
        }

        if (ActiveToolMode == ToolMode.Pointer)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindAncestor<System.Windows.Controls.Primitives.Thumb>(dep) is not null ||
                    IsInsideInteractiveElementButtonOrTextBox(dep))
                {
                    return;
                }
            }

            if (_isPointerDraggingObjects)
            {
                e.Cursor = Cursors.SizeAll;
                e.Handled = true;
                return;
            }

            if (_isPointerRectSelecting)
            {
                e.Cursor = Cursors.Arrow;
                e.Handled = true;
                return;
            }

            var canvasPos = e.GetPosition(InternalInkCanvas);
            if (IsPointInsideSelectionBox(canvasPos))
            {
                e.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
            else
            {
                e.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }
    }

    public void UpdateStrokeSelectionBorder()
    {
        if (StrokeSelectionBorder is null)
        {
            return;
        }

        var strokes = InternalInkCanvas.GetSelectedStrokes();
        if (ActiveToolMode != ToolMode.Pointer || strokes is null || strokes.Count == 0)
        {
            StrokeSelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var bounds = strokes.GetBounds();
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            StrokeSelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        const double padding = 4.0;
        Canvas.SetLeft(StrokeSelectionBorder, Math.Max(0.0, bounds.Left - padding));
        Canvas.SetTop(StrokeSelectionBorder, Math.Max(0.0, bounds.Top - padding));
        StrokeSelectionBorder.Width = bounds.Width + (padding * 2.0);
        StrokeSelectionBorder.Height = bounds.Height + (padding * 2.0);
        StrokeSelectionBorder.Visibility = Visibility.Visible;
    }

    public void PointerLeftDown(Point canvasPoint)
    {
        _pendingSingleSelectElement = null;
        _pendingSingleSelectStroke = null;
        _hasPointerDraggedBeyondThreshold = false;
        _pointerDragStartPoint = canvasPoint;

        var hitElement = HitTestTopmostElement(canvasPoint);
        var hitStroke = hitElement is null ? HitTestTopmostStroke(canvasPoint) : null;

        if (hitElement is not null)
        {
            if (hitElement.IsSelected)
            {
                _pendingSingleSelectElement = hitElement;
            }
            else
            {
                SelectSingleObject(hitElement, null);
            }

            _isPointerDraggingObjects = true;
            SnapshotSelectionState();
            CaptureMouse();
        }
        else if (hitStroke is not null)
        {
            var selectedStrokes = InternalInkCanvas.GetSelectedStrokes();
            if (selectedStrokes.Contains(hitStroke))
            {
                _pendingSingleSelectStroke = hitStroke;
            }
            else
            {
                SelectSingleObject(null, hitStroke);
            }

            _isPointerDraggingObjects = true;
            SnapshotSelectionState();
            CaptureMouse();
        }
        else if (IsPointInsideSelectionBox(canvasPoint))
        {
            // Clicked inside the selected box area (e.g. inside the stroke's dashed rectangle box)
            _isPointerDraggingObjects = true;
            SnapshotSelectionState();
            CaptureMouse();
        }
        else
        {
            // Empty space click -> clear selection & prepare rectangle marquee selection
            SelectSingleObject(null, null);

            _isPointerRectSelecting = true;
            if (PointerSelectionRectangle is not null)
            {
                Canvas.SetLeft(PointerSelectionRectangle, canvasPoint.X);
                Canvas.SetTop(PointerSelectionRectangle, canvasPoint.Y);
                PointerSelectionRectangle.Width = 0;
                PointerSelectionRectangle.Height = 0;
                PointerSelectionRectangle.Visibility = Visibility.Visible;
            }

            CaptureMouse();
        }

        UpdatePointerCursor(canvasPoint);
    }

    public void PointerLeftMove(Point canvasPoint)
    {
        if (_isPointerDraggingObjects)
        {
            Cursor = Cursors.SizeAll;
            var delta = canvasPoint - _pointerDragStartPoint;
            if (!_hasPointerDraggedBeyondThreshold && (Math.Abs(delta.X) > 3.0 || Math.Abs(delta.Y) > 3.0))
            {
                _hasPointerDraggedBeyondThreshold = true;
                _pendingSingleSelectElement = null;
                _pendingSingleSelectStroke = null;
            }

            if (_hasPointerDraggedBeyondThreshold)
            {
                var minInitialX = double.PositiveInfinity;
                var minInitialY = double.PositiveInfinity;

                foreach (var (_, initial) in _selectedElementInitialBounds)
                {
                    if (initial.X < minInitialX) minInitialX = initial.X;
                    if (initial.Y < minInitialY) minInitialY = initial.Y;
                }

                foreach (var (_, initialPoints) in _selectedStrokeInitialPoints)
                {
                    for (var i = 0; i < initialPoints.Count; i++)
                    {
                        var pt = initialPoints[i];
                        if (pt.X < minInitialX) minInitialX = pt.X;
                        if (pt.Y < minInitialY) minInitialY = pt.Y;
                    }
                }

                var clampedDeltaX = double.IsFinite(minInitialX) ? Math.Max(delta.X, -minInitialX) : delta.X;
                var clampedDeltaY = double.IsFinite(minInitialY) ? Math.Max(delta.Y, -minInitialY) : delta.Y;

                // Translate selected elements
                foreach (var (container, initial) in _selectedElementInitialBounds)
                {
                    var newX = Math.Max(0.0, initial.X + clampedDeltaX);
                    var newY = Math.Max(0.0, initial.Y + clampedDeltaY);
                    container.Element.X = newX;
                    container.Element.Y = newY;
                    InkCanvas.SetLeft(container, newX);
                    InkCanvas.SetTop(container, newY);
                }

                // Translate selected strokes
                foreach (var (stroke, initialPoints) in _selectedStrokeInitialPoints)
                {
                    var translatedPoints = new StylusPointCollection();
                    for (var i = 0; i < initialPoints.Count; i++)
                    {
                        var pt = initialPoints[i];
                        var newX = Math.Max(0.0, pt.X + clampedDeltaX);
                        var newY = Math.Max(0.0, pt.Y + clampedDeltaY);
                        translatedPoints.Add(new StylusPoint(newX, newY, pt.PressureFactor));
                    }
                    stroke.StylusPoints = translatedPoints;
                }

                UpdateStrokeSelectionBorder();
            }
        }
        else if (_isPointerRectSelecting)
        {
            Cursor = Cursors.Arrow;
            var x = Math.Min(_pointerDragStartPoint.X, canvasPoint.X);
            var y = Math.Min(_pointerDragStartPoint.Y, canvasPoint.Y);
            var width = Math.Abs(canvasPoint.X - _pointerDragStartPoint.X);
            var height = Math.Abs(canvasPoint.Y - _pointerDragStartPoint.Y);

            if (PointerSelectionRectangle is not null)
            {
                Canvas.SetLeft(PointerSelectionRectangle, x);
                Canvas.SetTop(PointerSelectionRectangle, y);
                PointerSelectionRectangle.Width = width;
                PointerSelectionRectangle.Height = height;
                PointerSelectionRectangle.Visibility = Visibility.Visible;
            }

            var selectRect = new Rect(x, y, width, height);
            SelectObjectsInRect(selectRect);
        }
    }

    public void SelectObjectsInRect(Rect selectRect)
    {
        _isSyncingSelection = true;
        try
        {
            var elementsToSelect = new List<UIElement>();
            var strokesToSelect = new StrokeCollection();

            var allowElements = ActiveLassoFilterMode is LassoFilterMode.AllObjects or LassoFilterMode.ElementsOnly;
            var allowStrokes = ActiveLassoFilterMode is LassoFilterMode.AllObjects or LassoFilterMode.StrokesOnly;

            if (allowElements && PageElements is not null)
            {
                foreach (var el in PageElements)
                {
                    var elRect = new Rect(el.X, el.Y, el.Width, el.Height);
                    var intersects = selectRect.IntersectsWith(elRect);
                    el.IsSelected = intersects;
                    if (!intersects)
                    {
                        el.IsEditing = false;
                    }
                    else if (_containers.TryGetValue(el, out var c))
                    {
                        elementsToSelect.Add(c);
                    }
                }
            }
            else if (PageElements is not null)
            {
                foreach (var el in PageElements)
                {
                    el.IsSelected = false;
                    el.IsEditing = false;
                }
            }

            if (allowStrokes && InternalInkCanvas.Strokes is not null)
            {
                foreach (var stroke in InternalInkCanvas.Strokes)
                {
                    var strokeBounds = stroke.GetBounds();
                    if (selectRect.IntersectsWith(strokeBounds) || stroke.HitTest(selectRect, 10))
                    {
                        strokesToSelect.Add(stroke);
                    }
                }
            }

            SelectedStrokes = strokesToSelect;
            if (DataContext is PageViewModel pageVm)
            {
                pageVm.SelectedStrokes = strokesToSelect;
            }

            InternalInkCanvas.Select(strokesToSelect, elementsToSelect);
            foreach (var c in _containers.Values)
            {
                c.RefreshAdornerAttachment();
            }
            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    public void PointerLeftUp(Point canvasPoint)
    {
        if (_isPointerDraggingObjects)
        {
            _isPointerDraggingObjects = false;
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            if (!_hasPointerDraggedBeyondThreshold)
            {
                // Simple click on an already-selected element or stroke -> collapse to strictly single selection
                if (_pendingSingleSelectElement is not null)
                {
                    SelectSingleObject(_pendingSingleSelectElement, null);
                }
                else if (_pendingSingleSelectStroke is not null)
                {
                    SelectSingleObject(null, _pendingSingleSelectStroke);
                }
            }
            else
            {
                // Drag completed -> record undo
                HandleSelectionTransformed();
            }

            _pendingSingleSelectElement = null;
            _pendingSingleSelectStroke = null;
            UpdatePointerCursor(canvasPoint);
        }
        else if (_isPointerRectSelecting)
        {
            _isPointerRectSelecting = false;
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            if (PointerSelectionRectangle is not null)
            {
                PointerSelectionRectangle.Visibility = Visibility.Collapsed;
            }

            SnapshotSelectionState();
            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(canvasPoint);
        }
    }

    private static bool IsInsideInteractiveElementButtonOrTextBox(DependencyObject? obj)
    {
        while (obj is not null)
        {
            if (obj is Button or TextBox or Wpf.Ui.Controls.TextBox)
            {
                return true;
            }

            obj = VisualTreeHelper.GetParent(obj);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static bool IsInsideElementOrAdorner(DependencyObject? obj)
    {
        while (obj is not null)
        {
            if (obj is CanvasElementContainer or System.Windows.Documents.Adorner)
            {
                return true;
            }

            obj = VisualTreeHelper.GetParent(obj);
        }

        return false;
    }
}

