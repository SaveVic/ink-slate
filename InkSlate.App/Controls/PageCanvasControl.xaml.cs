using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using InkSlate.App.ViewModels;
using InkSlate.Core.Ink;
using InkSlate.Core.Models;
using InkSlate.Core.Services;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.Controls;

public partial class PageCanvasControl : UserControl
{
    public static readonly DependencyProperty PageIdProperty =
        DependencyProperty.Register(
            nameof(PageId),
            typeof(Guid),
            typeof(PageCanvasControl),
            new PropertyMetadata(Guid.Empty, OnPageIdPropertyChanged));

    public static readonly DependencyProperty PageStrokesProperty =
        DependencyProperty.Register(
            nameof(PageStrokes),
            typeof(StrokeCollection),
            typeof(PageCanvasControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPageStrokesPropertyChanged));

    public static readonly DependencyProperty UndoRedoManagerProperty =
        DependencyProperty.Register(
            nameof(UndoRedoManager),
            typeof(UndoRedoManager),
            typeof(PageCanvasControl),
            new PropertyMetadata(null, OnUndoRedoManagerPropertyChanged));

    public static readonly DependencyProperty IsPageDirtyProperty =
        DependencyProperty.Register(
            nameof(IsPageDirty),
            typeof(bool),
            typeof(PageCanvasControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty InkDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(InkDrawingAttributes),
            typeof(DrawingAttributes),
            typeof(PageCanvasControl),
            new PropertyMetadata(null, OnInkDrawingAttributesPropertyChanged));

    public static readonly DependencyProperty ActiveToolModeProperty =
        DependencyProperty.Register(
            nameof(ActiveToolMode),
            typeof(ToolMode),
            typeof(PageCanvasControl),
            new PropertyMetadata(ToolMode.Pointer, OnActiveToolModePropertyChanged));

    public static readonly DependencyProperty ActiveEraserModeProperty =
        DependencyProperty.Register(
            nameof(ActiveEraserMode),
            typeof(EraserMode),
            typeof(PageCanvasControl),
            new PropertyMetadata(EraserMode.Stroke, OnActiveEraserModePropertyChanged));

    public static readonly DependencyProperty EraserSizeProperty =
        DependencyProperty.Register(
            nameof(EraserSize),
            typeof(double),
            typeof(PageCanvasControl),
            new PropertyMetadata(ToolbarViewModel.DefaultEraserSize, OnEraserSizePropertyChanged));

    public static readonly DependencyProperty PageElementsProperty =
        DependencyProperty.Register(
            nameof(PageElements),
            typeof(IList<CanvasElementViewModel>),
            typeof(PageCanvasControl),
            new PropertyMetadata(null, OnPageElementsPropertyChanged));

    public static readonly DependencyProperty RemoveElementCommandProperty =
        DependencyProperty.Register(
            nameof(RemoveElementCommand),
            typeof(ICommand),
            typeof(PageCanvasControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ActiveLassoFilterModeProperty =
        DependencyProperty.Register(
            nameof(ActiveLassoFilterMode),
            typeof(LassoFilterMode),
            typeof(PageCanvasControl),
            new PropertyMetadata(LassoFilterMode.AllObjects));

    private readonly Dictionary<CanvasElementViewModel, CanvasElementContainer> _containers = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? _observedElements;

    private bool _isSyncingStrokes;
    private bool _isSplittingStroke;
    private readonly HashSet<Stroke> _pendingOversizedStrokes = new(ReferenceEqualityComparer.Instance);
    private StrokeCollection? _subscribedStrokes;

    public PageCanvasControl()
    {
        InitializeComponent();

        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsTapFeedbackEnabled(this, false);
        Stylus.SetIsTouchFeedbackEnabled(this, false);

        Stylus.SetIsPressAndHoldEnabled(InternalInkCanvas, false);
        Stylus.SetIsFlicksEnabled(InternalInkCanvas, false);
        Stylus.SetIsTapFeedbackEnabled(InternalInkCanvas, false);
        Stylus.SetIsTouchFeedbackEnabled(InternalInkCanvas, false);

        InternalInkCanvas.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Color.FromRgb(0x1E, 0x1E, 0x1E),
            Width = 2.5,
            Height = 2.5,
            FitToCurve = true,
            IgnorePressure = false,
            StylusTip = StylusTip.Ellipse
        };

        AttachStrokesHandler(InternalInkCanvas.Strokes);
        InternalInkCanvas.StrokeCollected += OnStrokeCollected;
        InternalInkCanvas.SelectionChanging += OnInkCanvasSelectionChanging;
        InternalInkCanvas.SelectionChanged += OnInkCanvasSelectionChanged;
        InternalInkCanvas.SelectionMoved += OnInkCanvasSelectionMoved;
        InternalInkCanvas.SelectionResized += OnInkCanvasSelectionResized;
        RefreshEraserShape();
        UpdateEditingMode();
        AllowDrop = true;
        RequestBringIntoView += (_, e) => e.Handled = true;
        InternalInkCanvas.RequestBringIntoView += (_, e) => e.Handled = true;
    }

    public Guid PageId
    {
        get => (Guid)GetValue(PageIdProperty);
        set => SetValue(PageIdProperty, value);
    }

    public StrokeCollection? PageStrokes
    {
        get => (StrokeCollection?)GetValue(PageStrokesProperty);
        set => SetValue(PageStrokesProperty, value);
    }

    public UndoRedoManager? UndoRedoManager
    {
        get => (UndoRedoManager?)GetValue(UndoRedoManagerProperty);
        set => SetValue(UndoRedoManagerProperty, value);
    }

    public bool IsPageDirty
    {
        get => (bool)GetValue(IsPageDirtyProperty);
        set => SetValue(IsPageDirtyProperty, value);
    }

    public DrawingAttributes? InkDrawingAttributes
    {
        get => (DrawingAttributes?)GetValue(InkDrawingAttributesProperty);
        set => SetValue(InkDrawingAttributesProperty, value);
    }

    public ToolMode ActiveToolMode
    {
        get => (ToolMode)GetValue(ActiveToolModeProperty);
        set => SetValue(ActiveToolModeProperty, value);
    }

    public EraserMode ActiveEraserMode
    {
        get => (EraserMode)GetValue(ActiveEraserModeProperty);
        set => SetValue(ActiveEraserModeProperty, value);
    }

    public double EraserSize
    {
        get => (double)GetValue(EraserSizeProperty);
        set => SetValue(EraserSizeProperty, value);
    }

    public IList<CanvasElementViewModel>? PageElements
    {
        get => (IList<CanvasElementViewModel>?)GetValue(PageElementsProperty);
        set => SetValue(PageElementsProperty, value);
    }

    public ICommand? RemoveElementCommand
    {
        get => (ICommand?)GetValue(RemoveElementCommandProperty);
        set => SetValue(RemoveElementCommandProperty, value);
    }

    public LassoFilterMode ActiveLassoFilterMode
    {
        get => (LassoFilterMode)GetValue(ActiveLassoFilterModeProperty);
        set => SetValue(ActiveLassoFilterModeProperty, value);
    }

    public InkCanvas InkSurface => InternalInkCanvas;

    public Ellipse EraserCursor => EraserCursorIndicator;

    public IReadOnlyCollection<CanvasElementContainer> ElementContainers => _containers.Values;

    public CanvasElementContainer? GetContainer(CanvasElementViewModel vm) =>
        _containers.TryGetValue(vm, out var container) ? container : null;

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
                var originalIndex = PageElements.IndexOf(element);
                if (originalIndex >= 0)
                {
                    PageElements.RemoveAt(originalIndex);
                    IsPageDirty = true;
                    removedCount++;
                }
            }
        }

        return removedCount;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        var pos = e.GetPosition(this);

        if (ActiveToolMode == ToolMode.Eraser)
        {
            if (ActiveEraserMode == EraserMode.Point)
            {
                UpdateEraserCursor(pos);
            }
            else if (ActiveEraserMode == EraserMode.Object && e.LeftButton == MouseButtonState.Pressed)
            {
                EraseElementsAtPoint(pos);
            }
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (ActiveToolMode == ToolMode.Eraser && ActiveEraserMode == EraserMode.Object)
        {
            var pos = e.GetPosition(this);
            EraseElementsAtPoint(pos);
        }
        else if (ActiveToolMode == ToolMode.Text)
        {
            var hit = ElementContainers.FirstOrDefault(c => c.IsMouseOver);
            if (hit is null)
            {
                var pos = e.GetPosition(InternalInkCanvas);
                if (DataContext is PageViewModel pageVm)
                {
                    pageVm.AddTextElement(pos);
                    e.Handled = true;
                }
            }
        }
        else if (ActiveToolMode is not ToolMode.Pen and not ToolMode.Highlighter)
        {
            var isInteractive = IsInsideElementOrAdorner(e.OriginalSource as DependencyObject);
            if (!isInteractive)
            {
                var hit = ElementContainers.FirstOrDefault(c => c.IsMouseOver);
                if (hit is null)
                {
                    if (DataContext is PageViewModel pageVm)
                    {
                        pageVm.SelectSingleElement(null);
                    }
                    else if (PageElements is not null)
                    {
                        foreach (var el in PageElements.ToList())
                        {
                            el.IsSelected = false;
                            el.IsEditing = false;
                        }
                    }
                }
            }
        }
    }

    private static bool IsInsideElementOrAdorner(DependencyObject? obj)
    {
        while (obj is not null)
        {
            if (obj is CanvasElementContainer or CanvasElementAdorner or System.Windows.Controls.Primitives.Thumb)
            {
                return true;
            }

            obj = VisualTreeHelper.GetParent(obj);
        }

        return false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.OriginalSource is TextBox)
        {
            return;
        }

        if ((e.Key == Key.Delete || e.Key == Key.Back) && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (DeleteSelected())
            {
                e.Handled = true;
            }
        }
    }

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

        return deleted;
    }

    public bool DeleteSelectedStrokes()
    {
        var strokes = InternalInkCanvas.GetSelectedStrokes();
        if (strokes.Count > 0)
        {
            InternalInkCanvas.Strokes.Remove(strokes);
            return true;
        }
        return false;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HideEraserCursor();
    }

    protected override void OnPreviewStylusMove(StylusEventArgs e)
    {
        base.OnPreviewStylusMove(e);
        var pos = e.GetPosition(this);

        if (ActiveToolMode == ToolMode.Eraser)
        {
            if (ActiveEraserMode == EraserMode.Point)
            {
                UpdateEraserCursor(pos);
            }
            else if (ActiveEraserMode == EraserMode.Object)
            {
                EraseElementsAtPoint(pos);
            }
        }
    }

    protected override void OnPreviewStylusDown(StylusDownEventArgs e)
    {
        base.OnPreviewStylusDown(e);

        if (ActiveToolMode == ToolMode.Eraser && ActiveEraserMode == EraserMode.Object)
        {
            var pos = e.GetPosition(this);
            EraseElementsAtPoint(pos);
        }
    }

    protected override void OnStylusLeave(StylusEventArgs e)
    {
        base.OnStylusLeave(e);
        HideEraserCursor();
    }

    private static void OnPageIdPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control && e.NewValue is Guid newId)
        {
            foreach (var container in control._containers.Values)
            {
                container.PageId = newId;
            }
        }
    }

    private static void OnUndoRedoManagerPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control)
        {
            var mgr = e.NewValue as UndoRedoManager;
            foreach (var container in control._containers.Values)
            {
                container.UndoRedo = mgr;
            }
        }
    }

    private static void OnPageStrokesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control)
        {
            control.SyncStrokesFromProperty(e.NewValue as StrokeCollection);
        }
    }

    private static void OnInkDrawingAttributesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control && e.NewValue is DrawingAttributes attributes)
        {
            control.InternalInkCanvas.DefaultDrawingAttributes = attributes;
        }
    }

    private static void OnActiveToolModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control)
        {
            control.UpdateEditingMode();
        }
    }

    private static void OnPageElementsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control)
        {
            control.SyncElementsFromProperty(e.NewValue as IList<CanvasElementViewModel>);
        }
    }

    private static void OnActiveEraserModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control)
        {
            control.UpdateEditingMode();
        }
    }

    private static void OnEraserSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control)
        {
            control.RefreshEraserShape();
        }
    }

    private void UpdateEditingMode()
    {
        if (ActiveToolMode is ToolMode.Pen or ToolMode.Highlighter)
        {
            InternalInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            HideEraserCursor();
        }
        else if (ActiveToolMode == ToolMode.Eraser)
        {
            switch (ActiveEraserMode)
            {
                case EraserMode.Stroke:
                    InternalInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    HideEraserCursor();
                    break;
                case EraserMode.Point:
                    InternalInkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                    RefreshEraserShape();
                    break;
                case EraserMode.Object:
                    InternalInkCanvas.EditingMode = InkCanvasEditingMode.None;
                    HideEraserCursor();
                    break;
            }
        }
        else if (ActiveToolMode == ToolMode.Lasso)
        {
            InternalInkCanvas.EditingMode = InkCanvasEditingMode.Select;
            HideEraserCursor();
        }
        else
        {
            InternalInkCanvas.EditingMode = InkCanvasEditingMode.None;
            HideEraserCursor();
        }

        UpdateElementInteractivity();
    }

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

            if (newElements is INotifyCollectionChanged notify)
            {
                _observedElements = notify;
                _observedElements.CollectionChanged += OnElementsCollectionChanged;
            }
        }
    }

    private void ClearElementContainers()
    {
        foreach (var container in _containers.Values)
        {
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

        var container = new CanvasElementContainer(element, UndoRedoManager, PageId)
        {
            IsHitTestVisible = ActiveToolMode is ToolMode.Pointer or ToolMode.Text or ToolMode.Image or ToolMode.Lasso
        };
        _containers[element] = container;
        InternalInkCanvas.Children.Add(container);
    }

    private void RemoveElementContainer(CanvasElementViewModel element)
    {
        if (_containers.Remove(element, out var container))
        {
            container.Detach();
            InternalInkCanvas.Children.Remove(container);
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

            AttachStrokesHandler(target);
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

        // Defer oversized freshly-inked strokes; StrokeCollected will split and record them atomically.
        if (InternalInkCanvas.EditingMode == InkCanvasEditingMode.Ink &&
            e.Removed.Count == 0 &&
            e.Added.Count == 1 &&
            StrokeSplitter.IsOversized(e.Added[0]))
        {
            _pendingOversizedStrokes.Add(e.Added[0]);
            IsPageDirty = true;
            return;
        }

        IsPageDirty = true;

        var manager = UndoRedoManager;
        if (manager is not null && !manager.IsReplaying)
        {
            var target = _subscribedStrokes ?? InternalInkCanvas.Strokes;
            var op = new StrokesChangedOperation(PageId, target, e.Added, e.Removed);
            manager.Record(op);
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

    private void OnInlineTextBoxIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.IsVisible)
        {
            textBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
                textBox.CaretIndex = textBox.Text.Length;
            });
        }
    }

    public void SelectExclusive(CanvasElementViewModel? element)
    {
        if (DataContext is PageViewModel pageVm)
        {
            pageVm.SelectSingleElement(element);
        }
    }

    private void OnInlineTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is TextCanvasElementViewModel textVm)
        {
            textVm.IsEditing = false;

            if (string.IsNullOrWhiteSpace(textVm.Text))
            {
                if (RemoveElementCommand is not null && RemoveElementCommand.CanExecute(textVm))
                {
                    RemoveElementCommand.Execute(textVm);
                }
                else if (DataContext is PageViewModel pageVm)
                {
                    pageVm.RemoveElement(textVm);
                }
            }
        }
    }

    protected override void OnPreviewDragOver(DragEventArgs e)
    {
        base.OnPreviewDragOver(e);

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnPreviewDrop(DragEventArgs e)
    {
        base.OnPreviewDrop(e);

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files)
                {
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        continue;
                    }

                    var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                    {
                        var pos = e.GetPosition(InternalInkCanvas);
                        if (DataContext is PageViewModel pageVm)
                        {
                            try
                            {
                                var result = MediaIngestionService.IngestFile(file);
                                pageVm.RegisterAsset(result.AssetKey, result.SanitizedBytes);

                                var maxTargetW = Math.Min(pageVm.Width * 0.75, 800.0);
                                var maxTargetH = Math.Min(pageVm.Height * 0.75, 800.0);
                                var displayW = result.PixelWidth;
                                var displayH = result.PixelHeight;

                                if (displayW > maxTargetW)
                                {
                                    var scale = maxTargetW / displayW;
                                    displayW = maxTargetW;
                                    displayH *= scale;
                                }

                                if (displayH > maxTargetH)
                                {
                                    var scale = maxTargetH / displayH;
                                    displayH = maxTargetH;
                                    displayW *= scale;
                                }

                                displayW = Math.Max(20.0, displayW);
                                displayH = Math.Max(20.0, displayH);

                                pageVm.AddImageElement(
                                    result.AssetKey,
                                    result.FrozenBitmap,
                                    Math.Max(0.0, pos.X),
                                    Math.Max(0.0, pos.Y),
                                    displayW,
                                    displayH);
                            }
                            catch (Exception)
                            {
                                // Ingestion failed (oversized or invalid header)
                            }
                        }

                        e.Handled = true;
                        break;
                    }
                }
            }
        }
    }

    private readonly Dictionary<CanvasElementContainer, Rect> _selectedElementInitialBounds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Stroke, StylusPointCollection> _selectedStrokeInitialPoints = new(ReferenceEqualityComparer.Instance);

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
        SnapshotSelectionState();

        var selectedElements = InternalInkCanvas.GetSelectedElements();
        foreach (var container in _containers.Values)
        {
            container.Element.IsSelected = selectedElements.Contains(container);
            container.RefreshAdornerAttachment();
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
