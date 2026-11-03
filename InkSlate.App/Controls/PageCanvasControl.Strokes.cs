using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.App.ViewModels;
using InkSlate.Core.Ink;
using InkSlate.Core.UndoRedo;
using InkSlate.Core.Services;

namespace InkSlate.App.Controls;

public partial class PageCanvasControl
{
    private bool _isSyncingStrokes;
    private bool _isSplittingStroke;
    private readonly HashSet<Stroke> _pendingOversizedStrokes = new(ReferenceEqualityComparer.Instance);
    private StrokeCollection? _subscribedStrokes;

    private void SyncStrokesFromProperty(StrokeCollection? newStrokes)
    {
        DetachStrokesHandler();
        _pendingOversizedStrokes.Clear();

        _isSyncingStrokes = true;
        try
        {
            var target = newStrokes ?? [];
            if (!ReferenceEquals(InternalInkCanvas.Strokes, target))
            {
                InternalInkCanvas.Strokes = target;
            }

            if (_areCanvasHandlersAttached)
            {
                AttachStrokesHandler(target);
            }

            UpdateStrokeSelectionBorder();
        }
        finally
        {
            _isSyncingStrokes = false;
        }
    }

    private void AttachStrokesHandler(StrokeCollection strokes)
    {
        DetachStrokesHandler();
        _subscribedStrokes = strokes;
        _subscribedStrokes.StrokesChanged += OnInternalStrokesChanged;
    }

    private void DetachStrokesHandler()
    {
        if (_subscribedStrokes is not null)
        {
            _subscribedStrokes.StrokesChanged -= OnInternalStrokesChanged;
            _subscribedStrokes = null;
        }
    }

    private void OnInternalStrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        if (_isSyncingStrokes || _isSplittingStroke || (e.Added.Count == 0 && e.Removed.Count == 0))
        {
            return;
        }

        // Prevent duplicate Undo/Redo recording when a second hidden PageCanvasControl (e.g. inactive view mode) shares the same StrokeCollection
        if (!IsVisible && System.Windows.PresentationSource.FromVisual(this) is not null)
        {
            return;
        }

        // Defer oversized freshly-inked strokes; StrokeCollected will split and record them atomically.
        if (InternalInkCanvas.EditingMode == InkCanvasEditingMode.Ink &&
            e.Removed.Count == 0 &&
            e.Added.Count == 1 &&
            StrokeSplitter.IsOversized(e.Added[0]))
        {
            _pendingOversizedStrokes.Add(e.Added[0]);
            IsPageDirty = true;
            if (DataContext is PageViewModel pageVm)
            {
                pageVm.IsDirty = true;
            }
            return;
        }

        IsPageDirty = true;
        if (DataContext is PageViewModel vm)
        {
            vm.IsDirty = true;
        }

        var manager = UndoRedoManager;
        if (manager is not null && !manager.IsReplaying)
        {
            var target = _subscribedStrokes ?? InternalInkCanvas.Strokes;
            var op = new StrokesChangedOperation(PageId, target, e.Added, e.Removed);
            manager.Record(op);
        }

        if (e.Removed.Count > 0)
        {
            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
        }
    }

    public int SplitCollectedStroke(Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);

        var target = _subscribedStrokes ?? InternalInkCanvas.Strokes;
        var wasPending = _pendingOversizedStrokes.Remove(stroke);

        if (!StrokeSplitter.IsOversized(stroke) || !target.Contains(stroke))
        {
            return 0;
        }

        var chunks = StrokeSplitter.Split(stroke);
        var added = new StrokeCollection(chunks);

        _isSplittingStroke = true;
        try
        {
            target.Replace(stroke, added);
        }
        finally
        {
            _isSplittingStroke = false;
        }

        IsPageDirty = true;

        var manager = UndoRedoManager;
        if (manager is not null && !manager.IsReplaying)
        {
            var removed = wasPending ? [] : new StrokeCollection([stroke]);
            var op = new StrokesChangedOperation(PageId, target, added, removed);
            manager.Record(op);
        }

        return chunks.Count;
    }

    private void OnStrokeCollected(object? sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (e.Stroke is not null)
        {
            SplitCollectedStroke(e.Stroke);
        }
    }
}
