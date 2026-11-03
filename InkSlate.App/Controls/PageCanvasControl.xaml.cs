using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
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

    public static readonly DependencyProperty SelectedStrokesProperty =
        DependencyProperty.Register(
            nameof(SelectedStrokes),
            typeof(StrokeCollection),
            typeof(PageCanvasControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedStrokesPropertyChanged));

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

    private bool _areCanvasHandlersAttached;
    private Point? _lastCanvasPointerPosition;

    public bool AreCanvasHandlersAttached => _areCanvasHandlersAttached;
    public Point? LastCanvasPointerPosition => _lastCanvasPointerPosition;
    public InkCanvas InkSurface => InternalInkCanvas;

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

        AttachCanvasHandlers();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;

        RefreshEraserShape();
        UpdateEditingMode();
        AllowDrop = true;
        RequestBringIntoView += (_, e) => e.Handled = true;
        InternalInkCanvas.RequestBringIntoView += (_, e) => e.Handled = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => AttachCanvasHandlers();
    private void OnUnloaded(object sender, RoutedEventArgs e) => DetachCanvasHandlers();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is PageViewModel pageVm)
        {
            PageId = pageVm.Id;
            SyncStrokesFromProperty(pageVm.Strokes);
            SyncElementsFromProperty(pageVm.Elements);
            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
        }
        else if (e.NewValue is null)
        {
            SyncStrokesFromProperty(null);
            SyncElementsFromProperty(null);
            UpdateStrokeSelectionBorder();
        }
    }

    public void AttachCanvasHandlers()
    {
        if (_areCanvasHandlersAttached)
        {
            return;
        }

        InternalInkCanvas.StrokeCollected += OnStrokeCollected;
        InternalInkCanvas.SelectionChanging += OnInkCanvasSelectionChanging;
        InternalInkCanvas.SelectionChanged += OnInkCanvasSelectionChanged;
        InternalInkCanvas.SelectionMoved += OnInkCanvasSelectionMoved;
        InternalInkCanvas.SelectionResized += OnInkCanvasSelectionResized;

        var targetStrokes = PageStrokes ?? (DataContext as PageViewModel)?.Strokes ?? InternalInkCanvas.Strokes;
        SyncStrokesFromProperty(targetStrokes);

        var targetElements = PageElements ?? (DataContext as PageViewModel)?.Elements;
        SyncElementsFromProperty(targetElements);

        if (UndoRedoManager is not null)
        {
            UndoRedoManager.StateChanged -= OnUndoRedoStateChanged;
            UndoRedoManager.StateChanged += OnUndoRedoStateChanged;
        }

        _areCanvasHandlersAttached = true;
    }

    public void DetachCanvasHandlers()
    {
        if (!_areCanvasHandlersAttached)
        {
            return;
        }

        InternalInkCanvas.StrokeCollected -= OnStrokeCollected;
        InternalInkCanvas.SelectionChanging -= OnInkCanvasSelectionChanging;
        InternalInkCanvas.SelectionChanged -= OnInkCanvasSelectionChanged;
        InternalInkCanvas.SelectionMoved -= OnInkCanvasSelectionMoved;
        InternalInkCanvas.SelectionResized -= OnInkCanvasSelectionResized;

        if (UndoRedoManager is not null)
        {
            UndoRedoManager.StateChanged -= OnUndoRedoStateChanged;
        }

        DetachStrokesHandler();

        if (_observedElements is not null)
        {
            _observedElements.CollectionChanged -= OnElementsCollectionChanged;
            _observedElements = null;
        }

        foreach (var element in _containers.Keys)
        {
            element.PropertyChanged -= OnElementPropertyChanged;
        }

        _selectedElementInitialBounds.Clear();
        _selectedStrokeInitialPoints.Clear();
        _pendingOversizedStrokes.Clear();

        _areCanvasHandlersAttached = false;
    }

    private void OnUndoRedoStateChanged(object? sender, EventArgs e)
    {
        SnapshotSelectionState();
        UpdateStrokeSelectionBorder();
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

    public StrokeCollection? SelectedStrokes
    {
        get => (StrokeCollection?)GetValue(SelectedStrokesProperty);
        set => SetValue(SelectedStrokesProperty, value);
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

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        var pos = e.GetPosition(this);
        var canvasPos = e.GetPosition(InternalInkCanvas);
        _lastCanvasPointerPosition = canvasPos;

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
        else if (ActiveToolMode == ToolMode.Pointer)
        {
            if (_isPointerDraggingObjects || _isPointerRectSelecting)
            {
                PointerLeftMove(canvasPos);
                e.Handled = true;
            }
            else
            {
                UpdatePointerCursor(canvasPos);
            }
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);

        if (ActiveToolMode == ToolMode.Text)
        {
            var isInteractive = IsInsideElementOrAdorner(e.OriginalSource as DependencyObject);
            if (!isInteractive)
            {
                if (DataContext is PageViewModel pageVm)
                {
                    pageVm.AddTextElement(pos);
                    e.Handled = true;
                }
            }
        }
        else if (ActiveToolMode == ToolMode.Pointer)
        {
            var dep = e.OriginalSource as DependencyObject;
            if (FindAncestor<System.Windows.Controls.Primitives.Thumb>(dep) is not null ||
                IsInsideInteractiveElementButtonOrTextBox(dep))
            {
                return;
            }

            var canvasPos = e.GetPosition(InternalInkCanvas);
            if (e.ClickCount == 2 && HitTestTopmostElement(canvasPos) is TextCanvasElementViewModel textVm)
            {
                SelectSingleObject(textVm, null);
                textVm.IsEditing = true;
                _isPointerDraggingObjects = false;
                if (IsMouseCaptured)
                {
                    ReleaseMouseCapture();
                }
                e.Handled = true;
                return;
            }

            PointerLeftDown(canvasPos);
            e.Handled = true;
        }
        else if (ActiveToolMode is not ToolMode.Pen and not ToolMode.Highlighter)
        {
            if (ActiveToolMode == ToolMode.Eraser && ActiveEraserMode == EraserMode.Object)
            {
                EraseElementsAtPoint(pos);
            }

            var isInteractive = IsInsideElementOrAdorner(e.OriginalSource as DependencyObject);
            if (!isInteractive)
            {
                var hit = HitTestTopmostElement(pos);
                if (hit is null)
                {
                    if (DataContext is PageViewModel pageVm)
                    {
                        pageVm.SelectSingleElement(null);
                        pageVm.SelectedStrokes = null;
                    }
                    else if (PageElements is not null)
                    {
                        foreach (var el in PageElements.ToList())
                        {
                            el.IsSelected = false;
                            el.IsEditing = false;
                        }
                    }

                    _isSyncingSelection = true;
                    try
                    {
                        SelectedStrokes = null;
                        InternalInkCanvas.Select(null, null);
                    }
                    finally
                    {
                        _isSyncingSelection = false;
                    }
                }
            }
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (ActiveToolMode == ToolMode.Pointer && (_isPointerDraggingObjects || _isPointerRectSelecting))
        {
            var canvasPos = e.GetPosition(InternalInkCanvas);
            PointerLeftUp(canvasPos);
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (ActiveToolMode == ToolMode.Pointer && (_isPointerDraggingObjects || _isPointerRectSelecting))
        {
            var canvasPos = e.GetPosition(InternalInkCanvas);
            PointerLeftUp(canvasPos);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _lastCanvasPointerPosition = null;
        HideEraserCursor();
        if (ActiveToolMode == ToolMode.Pointer && !_isPointerDraggingObjects && !_isPointerRectSelecting)
        {
            Cursor = null;
        }
    }

    protected override void OnStylusDown(StylusDownEventArgs e)
    {
        base.OnStylusDown(e);
        var pos = e.GetPosition(this);
        _lastCanvasPointerPosition = e.GetPosition(InternalInkCanvas);
        if (ActiveToolMode == ToolMode.Eraser && ActiveEraserMode == EraserMode.Object)
        {
            EraseElementsAtPoint(pos);
        }
    }

    protected override void OnStylusMove(StylusEventArgs e)
    {
        base.OnStylusMove(e);
        var pos = e.GetPosition(this);
        _lastCanvasPointerPosition = e.GetPosition(InternalInkCanvas);
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

    protected override void OnStylusUp(StylusEventArgs e)
    {
        base.OnStylusUp(e);
        var pos = e.GetPosition(this);
        _lastCanvasPointerPosition = e.GetPosition(InternalInkCanvas);
        if (ActiveToolMode == ToolMode.Eraser && ActiveEraserMode == EraserMode.Object)
        {
            EraseElementsAtPoint(pos);
        }
    }

    protected override void OnStylusLeave(StylusEventArgs e)
    {
        base.OnStylusLeave(e);
        _lastCanvasPointerPosition = null;
        HideEraserCursor();
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
            if (DeleteSelection())
            {
                e.Handled = true;
            }
        }
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
            if (e.OldValue is UndoRedoManager oldMgr)
            {
                oldMgr.StateChanged -= control.OnUndoRedoStateChanged;
            }

            var mgr = e.NewValue as UndoRedoManager;
            if (mgr is not null && control._areCanvasHandlersAttached)
            {
                mgr.StateChanged -= control.OnUndoRedoStateChanged;
                mgr.StateChanged += control.OnUndoRedoStateChanged;
            }

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

    private static void OnSelectedStrokesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageCanvasControl control)
        {
            control.SyncSelectionFromViewModel(e.NewValue as StrokeCollection);
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

        if (ActiveToolMode != ToolMode.Pointer)
        {
            if (StrokeSelectionBorder is not null)
            {
                StrokeSelectionBorder.Visibility = Visibility.Collapsed;
            }
            Cursor = null;
        }
        else
        {
            UpdateStrokeSelectionBorder();
            UpdatePointerCursor(Mouse.GetPosition(InternalInkCanvas));
        }

        UpdateElementInteractivity();
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
        _isSyncingSelection = true;
        try
        {
            if (DataContext is PageViewModel pageVm)
            {
                pageVm.SelectSingleElement(element);
                pageVm.SelectedStrokes = null;
            }

            SelectedStrokes = null;
            var elements = new List<UIElement>();
            if (element is not null && _containers.TryGetValue(element, out var container))
            {
                elements.Add(container);
            }
            InternalInkCanvas.Select(new StrokeCollection(), elements);
            foreach (var c in _containers.Values)
            {
                c.RefreshAdornerAttachment();
            }
            UpdateStrokeSelectionBorder();
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void OnInlineTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is TextCanvasElementViewModel textVm)
        {
            textVm.IsEditing = false;

            if (string.IsNullOrWhiteSpace(textVm.Text))
            {
                if (DataContext is PageViewModel pageVm)
                {
                    pageVm.RemoveEmptyTextElement(textVm);
                }
                else if (RemoveElementCommand is not null && RemoveElementCommand.CanExecute(textVm))
                {
                    RemoveElementCommand.Execute(textVm);
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
}
