using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.App.ViewModels;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.Controls;

public sealed class CanvasElementAdorner : Adorner
{
    private const double ThumbSize = 10.0;
    private const double MinDimension = 20.0;

    public static readonly SolidColorBrush AccentBorderBrush;
    public static readonly Pen BorderPen;
    public static readonly SolidColorBrush ThumbBackgroundBrush;

    static CanvasElementAdorner()
    {
        AccentBorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xD4));
        AccentBorderBrush.Freeze();

        BorderPen = new Pen(AccentBorderBrush, 1.5)
        {
            DashStyle = DashStyles.Dash
        };
        BorderPen.Freeze();

        ThumbBackgroundBrush = (SolidColorBrush)Brushes.White.Clone();
        ThumbBackgroundBrush.Freeze();
    }

    private readonly VisualCollection _visualChildren;
    private readonly Thumb _topLeftThumb;
    private readonly Thumb _topRightThumb;
    private readonly Thumb _bottomRightThumb;
    private readonly Thumb _bottomLeftThumb;

    private Rect _initialBounds;
    private Rect? _initialCrop;
    private double _totalDeltaX;
    private double _totalDeltaY;

    public CanvasElementAdorner(
        UIElement adornedElement,
        CanvasElementViewModel elementVm,
        UndoRedoManager? undoRedo,
        Guid pageId)
        : base(adornedElement)
    {
        ArgumentNullException.ThrowIfNull(adornedElement);
        ArgumentNullException.ThrowIfNull(elementVm);

        ElementViewModel = elementVm;
        UndoRedo = undoRedo;
        PageId = pageId;

        _visualChildren = new VisualCollection(this);

        _topLeftThumb = CreateThumb(Cursors.SizeNWSE);
        _topRightThumb = CreateThumb(Cursors.SizeNESW);
        _bottomRightThumb = CreateThumb(Cursors.SizeNWSE);
        _bottomLeftThumb = CreateThumb(Cursors.SizeNESW);

        HookThumbEvents(_topLeftThumb, OnTopLeftDragDelta);
        HookThumbEvents(_topRightThumb, OnTopRightDragDelta);
        HookThumbEvents(_bottomRightThumb, OnBottomRightDragDelta);
        HookThumbEvents(_bottomLeftThumb, OnBottomLeftDragDelta);

        _visualChildren.Add(_topLeftThumb);
        _visualChildren.Add(_topRightThumb);
        _visualChildren.Add(_bottomRightThumb);
        _visualChildren.Add(_bottomLeftThumb);
    }

    public CanvasElementViewModel ElementViewModel { get; }
    public UndoRedoManager? UndoRedo { get; }
    public Guid PageId { get; }

    public bool IsCropMode { get; set; }

    protected override int VisualChildrenCount => _visualChildren.Count;

    protected override Visual GetVisualChild(int index) => _visualChildren[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        var half = ThumbSize / 2.0;

        _topLeftThumb.Arrange(new Rect(-half, -half, ThumbSize, ThumbSize));
        _topRightThumb.Arrange(new Rect(finalSize.Width - half, -half, ThumbSize, ThumbSize));
        _bottomRightThumb.Arrange(new Rect(finalSize.Width - half, finalSize.Height - half, ThumbSize, ThumbSize));
        _bottomLeftThumb.Arrange(new Rect(-half, finalSize.Height - half, ThumbSize, ThumbSize));

        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var rect = new Rect(0, 0, AdornedElement.RenderSize.Width, AdornedElement.RenderSize.Height);
        drawingContext.DrawRectangle(null, BorderPen, rect);
    }

    public void Detach()
    {
        UnhookThumbEvents(_topLeftThumb, OnTopLeftDragDelta);
        UnhookThumbEvents(_topRightThumb, OnTopRightDragDelta);
        UnhookThumbEvents(_bottomRightThumb, OnBottomRightDragDelta);
        UnhookThumbEvents(_bottomLeftThumb, OnBottomLeftDragDelta);
    }

    private static Thumb CreateThumb(Cursor cursor)
    {
        var thumb = new Thumb
        {
            Cursor = cursor,
            Width = ThumbSize,
            Height = ThumbSize,
            Background = ThumbBackgroundBrush,
            BorderBrush = AccentBorderBrush,
            BorderThickness = new Thickness(1.5)
        };

        var template = new System.Windows.Controls.ControlTemplate(typeof(Thumb));
        var factory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        factory.SetValue(System.Windows.Shapes.Shape.FillProperty, new TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty));
        factory.SetValue(System.Windows.Shapes.Shape.StrokeProperty, new TemplateBindingExtension(System.Windows.Controls.Control.BorderBrushProperty));
        factory.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.5);
        template.VisualTree = factory;
        thumb.Template = template;

        return thumb;
    }

    private void HookThumbEvents(Thumb thumb, DragDeltaEventHandler onDelta)
    {
        thumb.DragStarted += OnThumbDragStarted;
        thumb.DragDelta += onDelta;
        thumb.DragCompleted += OnThumbDragCompleted;
    }

    private void UnhookThumbEvents(Thumb thumb, DragDeltaEventHandler onDelta)
    {
        thumb.DragStarted -= OnThumbDragStarted;
        thumb.DragDelta -= onDelta;
        thumb.DragCompleted -= OnThumbDragCompleted;
    }

    private void OnThumbDragStarted(object sender, DragStartedEventArgs e)
    {
        _totalDeltaX = 0;
        _totalDeltaY = 0;
        _initialBounds = new Rect(ElementViewModel.X, ElementViewModel.Y, ElementViewModel.Width, ElementViewModel.Height);
        if (ElementViewModel is ImageCanvasElementViewModel imgVm)
        {
            _initialCrop = imgVm.CropRect;
        }
        else
        {
            _initialCrop = null;
        }
    }

    private void OnThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _totalDeltaX = 0;
        _totalDeltaY = 0;
        var newBounds = new Rect(ElementViewModel.X, ElementViewModel.Y, ElementViewModel.Width, ElementViewModel.Height);
        Rect? newCrop = ElementViewModel is ImageCanvasElementViewModel imgVm ? imgVm.CropRect : null;

        if (newBounds != _initialBounds || newCrop != _initialCrop)
        {
            if (UndoRedo is not null && !UndoRedo.IsReplaying)
            {
                var op = new ElementTransformedOperation(PageId, ElementViewModel.Model, _initialBounds, newBounds, _initialCrop, newCrop);
                UndoRedo.Record(op);
            }
        }
    }

    private void OnBottomRightDragDelta(object sender, DragDeltaEventArgs e)
    {
        EnsureInitialBounds();
        _totalDeltaX += e.HorizontalChange;
        _totalDeltaY += e.VerticalChange;

        if (ElementViewModel is ImageCanvasElementViewModel && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
        {
            var aspect = Math.Max(0.01, _initialBounds.Width / Math.Max(1.0, _initialBounds.Height));
            var newW = Math.Max(MinDimension, _initialBounds.Width + _totalDeltaX);
            var newH = Math.Max(MinDimension, newW / aspect);
            ElementViewModel.Width = newW;
            ElementViewModel.Height = newH;
        }
        else
        {
            ElementViewModel.Width = Math.Max(MinDimension, _initialBounds.Width + _totalDeltaX);
            ElementViewModel.Height = Math.Max(MinDimension, _initialBounds.Height + _totalDeltaY);
        }
    }

    private void EnsureInitialBounds()
    {
        if (_initialBounds.IsEmpty || _initialBounds.Width <= 0 || _initialBounds.Height <= 0)
        {
            _initialBounds = new Rect(ElementViewModel.X, ElementViewModel.Y, ElementViewModel.Width, ElementViewModel.Height);
            if (ElementViewModel is ImageCanvasElementViewModel imgVm)
            {
                _initialCrop = imgVm.CropRect;
            }
        }
    }

    private void OnBottomLeftDragDelta(object sender, DragDeltaEventArgs e)
    {
        EnsureInitialBounds();
        _totalDeltaX += e.HorizontalChange;
        _totalDeltaY += e.VerticalChange;

        if (ElementViewModel is ImageCanvasElementViewModel && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
        {
            var aspect = Math.Max(0.01, _initialBounds.Width / Math.Max(1.0, _initialBounds.Height));
            var newW = Math.Max(MinDimension, _initialBounds.Width - _totalDeltaX);
            var newH = Math.Max(MinDimension, newW / aspect);
            ElementViewModel.X = _initialBounds.Right - newW;
            ElementViewModel.Width = newW;
            ElementViewModel.Height = newH;
        }
        else
        {
            var newW = Math.Max(MinDimension, _initialBounds.Width - _totalDeltaX);
            ElementViewModel.X = _initialBounds.Right - newW;
            ElementViewModel.Width = newW;
            ElementViewModel.Height = Math.Max(MinDimension, _initialBounds.Height + _totalDeltaY);
        }
    }

    private void OnTopRightDragDelta(object sender, DragDeltaEventArgs e)
    {
        EnsureInitialBounds();
        _totalDeltaX += e.HorizontalChange;
        _totalDeltaY += e.VerticalChange;

        if (ElementViewModel is ImageCanvasElementViewModel && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
        {
            var aspect = Math.Max(0.01, _initialBounds.Width / Math.Max(1.0, _initialBounds.Height));
            var newW = Math.Max(MinDimension, _initialBounds.Width + _totalDeltaX);
            var newH = Math.Max(MinDimension, newW / aspect);
            ElementViewModel.Y = _initialBounds.Bottom - newH;
            ElementViewModel.Width = newW;
            ElementViewModel.Height = newH;
        }
        else
        {
            var newH = Math.Max(MinDimension, _initialBounds.Height - _totalDeltaY);
            ElementViewModel.Y = _initialBounds.Bottom - newH;
            ElementViewModel.Width = Math.Max(MinDimension, _initialBounds.Width + _totalDeltaX);
            ElementViewModel.Height = newH;
        }
    }

    private void OnTopLeftDragDelta(object sender, DragDeltaEventArgs e)
    {
        EnsureInitialBounds();
        _totalDeltaX += e.HorizontalChange;
        _totalDeltaY += e.VerticalChange;

        if (ElementViewModel is ImageCanvasElementViewModel && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
        {
            var aspect = Math.Max(0.01, _initialBounds.Width / Math.Max(1.0, _initialBounds.Height));
            var newW = Math.Max(MinDimension, _initialBounds.Width - _totalDeltaX);
            var newH = Math.Max(MinDimension, newW / aspect);
            ElementViewModel.X = _initialBounds.Right - newW;
            ElementViewModel.Y = _initialBounds.Bottom - newH;
            ElementViewModel.Width = newW;
            ElementViewModel.Height = newH;
        }
        else
        {
            var newW = Math.Max(MinDimension, _initialBounds.Width - _totalDeltaX);
            var newH = Math.Max(MinDimension, _initialBounds.Height - _totalDeltaY);
            ElementViewModel.X = _initialBounds.Right - newW;
            ElementViewModel.Y = _initialBounds.Bottom - newH;
            ElementViewModel.Width = newW;
            ElementViewModel.Height = newH;
        }
    }
}

