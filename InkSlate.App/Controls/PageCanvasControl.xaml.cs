using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.Core.Models;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.Controls;

public partial class PageCanvasControl : UserControl
{
    public static readonly DependencyProperty PageIdProperty =
        DependencyProperty.Register(
            nameof(PageId),
            typeof(Guid),
            typeof(PageCanvasControl),
            new PropertyMetadata(Guid.Empty));

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
            new PropertyMetadata(null));

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
            new PropertyMetadata(ToolMode.Pen, OnActiveToolModePropertyChanged));

    private bool _isSyncingStrokes;
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

    public InkCanvas InkSurface => InternalInkCanvas;

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
        if (d is PageCanvasControl control && e.NewValue is ToolMode mode)
        {
            control.UpdateEditingMode(mode);
        }
    }

    private void UpdateEditingMode(ToolMode mode)
    {
        InternalInkCanvas.EditingMode = mode switch
        {
            ToolMode.Pen or ToolMode.Highlighter => InkCanvasEditingMode.Ink,
            ToolMode.Eraser => InkCanvasEditingMode.EraseByStroke,
            ToolMode.Lasso => InkCanvasEditingMode.Select,
            _ => InkCanvasEditingMode.None
        };
    }

    private void SyncStrokesFromProperty(StrokeCollection? newStrokes)
    {
        DetachStrokesHandler();

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
        if (_isSyncingStrokes || (e.Added.Count == 0 && e.Removed.Count == 0))
        {
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
}
