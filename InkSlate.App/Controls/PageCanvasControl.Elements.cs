using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.Controls;

public partial class PageCanvasControl
{
    private readonly Dictionary<CanvasElementViewModel, CanvasElementContainer> _containers = new();
    private readonly Dictionary<CanvasElementContainer, Rect> _selectedElementInitialBounds = new();
    private readonly Dictionary<Stroke, StylusPointCollection> _selectedStrokeInitialPoints = new();
    private INotifyCollectionChanged? _observedElements;
    private bool _isSyncingSelection;

    public IReadOnlyCollection<CanvasElementContainer> ElementContainers => _containers.Values;

    public CanvasElementContainer? GetContainer(CanvasElementViewModel vm) =>
        _containers.TryGetValue(vm, out var container) ? container : null;

    private void SyncElementsFromProperty(IList<CanvasElementViewModel>? newElements)
    {
        if (_observedElements is not null)
        {
            _observedElements.CollectionChanged -= OnElementsCollectionChanged;
            _observedElements = null;
        }

        ClearElementContainers();

        if (newElements is not null)
        {
            foreach (var element in newElements)
            {
                AddElementContainer(element);
            }

            if (_areCanvasHandlersAttached && newElements is INotifyCollectionChanged notify)
            {
                _observedElements = notify;
                _observedElements.CollectionChanged += OnElementsCollectionChanged;
            }
        }
    }

    private void ClearElementContainers()
    {
        foreach (var (element, container) in _containers)
        {
            element.PropertyChanged -= OnElementPropertyChanged;
            container.Detach();
            InternalInkCanvas.Children.Remove(container);
        }
        _containers.Clear();
    }

    private void AddElementContainer(CanvasElementViewModel element)
    {
        if (_containers.ContainsKey(element))
        {
            return;
        }

        element.PropertyChanged += OnElementPropertyChanged;

        var container = new CanvasElementContainer(element, UndoRedoManager, PageId)
        {
            IsHitTestVisible = ActiveToolMode is ToolMode.Pointer or ToolMode.Text or ToolMode.Image or ToolMode.Lasso
        };
        _containers[element] = container;
        InternalInkCanvas.Children.Add(container);
    }

    private void RemoveElementContainer(CanvasElementViewModel element)
    {
        element.PropertyChanged -= OnElementPropertyChanged;
        if (_containers.Remove(element, out var container))
        {
            container.Detach();
            InternalInkCanvas.Children.Remove(container);
        }

        UpdateStrokeSelectionBorder();
        UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
    }

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasElementViewModel.IsSelected))
        {
            SyncSelectionFromViewModel();
        }
    }

    private void OnElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    foreach (CanvasElementViewModel item in e.NewItems)
                    {
                        AddElementContainer(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                {
                    foreach (CanvasElementViewModel item in e.OldItems)
                    {
                        RemoveElementContainer(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                {
                    foreach (CanvasElementViewModel item in e.OldItems)
                    {
                        RemoveElementContainer(item);
                    }
                }
                if (e.NewItems is not null)
                {
                    foreach (CanvasElementViewModel item in e.NewItems)
                    {
                        AddElementContainer(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                SyncElementsFromProperty(PageElements);
                break;

            case NotifyCollectionChangedAction.Move:
                break;
        }
    }

    private void UpdateElementInteractivity()
    {
        var interactive = ActiveToolMode is ToolMode.Pointer or ToolMode.Text or ToolMode.Image or ToolMode.Lasso;
        foreach (var container in _containers.Values)
        {
            container.IsHitTestVisible = interactive;
            container.RefreshAdornerAttachment();
        }
    }

    public void SyncSelectionFromViewModel(StrokeCollection? newSelectedStrokes = null)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            var strokes = newSelectedStrokes ?? SelectedStrokes ?? (DataContext as PageViewModel)?.SelectedStrokes ?? new StrokeCollection();
            var elements = new List<UIElement>();
            if (PageElements is not null)
            {
                foreach (var el in PageElements)
                {
                    if (el.IsSelected && _containers.TryGetValue(el, out var container))
                    {
                        elements.Add(container);
                    }
                }
            }

            InternalInkCanvas.Select(strokes, elements);
            foreach (var container in _containers.Values)
            {
                container.RefreshAdornerAttachment();
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

    private void OnInkCanvasSelectionChanging(object? sender, InkCanvasSelectionChangingEventArgs e)
    {
        switch (ActiveLassoFilterMode)
        {
            case LassoFilterMode.StrokesOnly:
                e.SetSelectedElements([]);
                break;

            case LassoFilterMode.ElementsOnly:
                e.SetSelectedStrokes(new StrokeCollection());
                break;

            case LassoFilterMode.AllObjects:
                break;
        }
    }

    private void OnInkCanvasSelectionChanged(object? sender, EventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            SnapshotSelectionState();

            var selectedElements = InternalInkCanvas.GetSelectedElements();
            foreach (var container in _containers.Values)
            {
                container.Element.IsSelected = selectedElements.Contains(container);
                container.RefreshAdornerAttachment();
            }

            var strokes = InternalInkCanvas.GetSelectedStrokes();
            SelectedStrokes = strokes;
            if (DataContext is PageViewModel pageVm)
            {
                pageVm.SelectedStrokes = strokes;
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void SnapshotSelectionState()
    {
        _selectedElementInitialBounds.Clear();
        foreach (var el in InternalInkCanvas.GetSelectedElements())
        {
            if (el is CanvasElementContainer container)
            {
                _selectedElementInitialBounds[container] = new Rect(
                    container.Element.X,
                    container.Element.Y,
                    container.Element.Width,
                    container.Element.Height);
            }
        }

        _selectedStrokeInitialPoints.Clear();
        foreach (var stroke in InternalInkCanvas.GetSelectedStrokes())
        {
            _selectedStrokeInitialPoints[stroke] = stroke.StylusPoints.Clone();
        }
    }

    private void OnInkCanvasSelectionMoved(object? sender, EventArgs e) => HandleSelectionTransformed();
    private void OnInkCanvasSelectionResized(object? sender, EventArgs e) => HandleSelectionTransformed();

    private void HandleSelectionTransformed()
    {
        var selectedElements = InternalInkCanvas.GetSelectedElements();
        var selectedStrokes = InternalInkCanvas.GetSelectedStrokes();

        var elementChanges = new List<ElementTransformChange>();
        var strokeChanges = new List<StrokeTransformChange>();

        foreach (var el in selectedElements)
        {
            if (el is CanvasElementContainer container)
            {
                var newX = InkCanvas.GetLeft(container);
                var newY = InkCanvas.GetTop(container);
                var newW = container.Width;
                var newH = container.Height;
                var newBounds = new Rect(newX, newY, newW, newH);

                if (_selectedElementInitialBounds.TryGetValue(container, out var oldBounds))
                {
                    if (newBounds != oldBounds)
                    {
                        elementChanges.Add(new ElementTransformChange(container.Element.Model, oldBounds, newBounds));
                        container.Element.X = newX;
                        container.Element.Y = newY;
                        container.Element.Width = newW;
                        container.Element.Height = newH;
                    }
                }
            }
        }

        foreach (var stroke in selectedStrokes)
        {
            if (_selectedStrokeInitialPoints.TryGetValue(stroke, out var oldPoints))
            {
                var newPoints = stroke.StylusPoints.Clone();
                if (!AreStylusPointsEqual(oldPoints, newPoints))
                {
                    strokeChanges.Add(new StrokeTransformChange(stroke, oldPoints, newPoints));
                }
            }
        }

        if (elementChanges.Count > 0 || strokeChanges.Count > 0)
        {
            IsPageDirty = true;
            if (UndoRedoManager is not null && !UndoRedoManager.IsReplaying)
            {
                UndoRedoManager.Record(new ElementTransformedOperation(PageId, elementChanges, strokeChanges));
            }

            SnapshotSelectionState();
        }
    }

    private static bool AreStylusPointsEqual(StylusPointCollection a, StylusPointCollection b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            var pa = a[i];
            var pb = b[i];
            if (Math.Abs(pa.X - pb.X) > 0.001 || Math.Abs(pa.Y - pb.Y) > 0.001)
            {
                return false;
            }
        }
        return true;
    }
}
