using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.Core.Models;

namespace InkSlate.App.Controls;

public sealed class ZoomScrollViewer : ScrollViewer
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 5.0;
    public const double DefaultZoom = 1.0;
    public const double ZoomInFactor = 1.1;
    public const double ZoomOutFactor = 0.9;

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(
            nameof(ZoomLevel),
            typeof(double),
            typeof(ZoomScrollViewer),
            new FrameworkPropertyMetadata(
                DefaultZoom,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnZoomLevelPropertyChanged,
                CoerceZoomLevel));

    public static readonly DependencyProperty CurrentViewModeProperty =
        DependencyProperty.Register(
            nameof(CurrentViewMode),
            typeof(ViewMode),
            typeof(ZoomScrollViewer),
            new PropertyMetadata(ViewMode.Consecutive));

    public static readonly DependencyProperty ActiveToolModeProperty =
        DependencyProperty.Register(
            nameof(ActiveToolMode),
            typeof(ToolMode),
            typeof(ZoomScrollViewer),
            new PropertyMetadata(ToolMode.Pointer, OnActiveToolModePropertyChanged));

    public static readonly DependencyProperty PreviousPageCommandProperty =
        DependencyProperty.Register(
            nameof(PreviousPageCommand),
            typeof(ICommand),
            typeof(ZoomScrollViewer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty NextPageCommandProperty =
        DependencyProperty.Register(
            nameof(NextPageCommand),
            typeof(ICommand),
            typeof(ZoomScrollViewer),
            new PropertyMetadata(null));

    private readonly ScaleTransform _scaleTransform = new(DefaultZoom, DefaultZoom);
    private Point? _pendingZoomAnchor;
    private bool _isPanning;
    private bool _isSpaceKeyDown;
    private bool _isStylusActive;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

    public ZoomScrollViewer()
    {
        Focusable = true;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        PanningMode = PanningMode.None;
        IsManipulationEnabled = true;
        Stylus.SetIsPressAndHoldEnabled(this, false);
    }

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public ViewMode CurrentViewMode
    {
        get => (ViewMode)GetValue(CurrentViewModeProperty);
        set => SetValue(CurrentViewModeProperty, value);
    }

    public ToolMode ActiveToolMode
    {
        get => (ToolMode)GetValue(ActiveToolModeProperty);
        set => SetValue(ActiveToolModeProperty, value);
    }

    public ICommand? PreviousPageCommand
    {
        get => (ICommand?)GetValue(PreviousPageCommandProperty);
        set => SetValue(PreviousPageCommandProperty, value);
    }

    public ICommand? NextPageCommand
    {
        get => (ICommand?)GetValue(NextPageCommandProperty);
        set => SetValue(NextPageCommandProperty, value);
    }

    public bool IsPanning => _isPanning;
    public double TargetHorizontalOffset { get; private set; }
    public double TargetVerticalOffset { get; private set; }
    public ScaleTransform ContentScaleTransform => _scaleTransform;

    public void ZoomAtPoint(double zoomMultiplier, Point viewportPoint)
    {
        if (!double.IsFinite(zoomMultiplier) || zoomMultiplier <= 0.0)
        {
            return;
        }

        var targetZoom = Math.Clamp(ZoomLevel * zoomMultiplier, MinZoom, MaxZoom);
        _pendingZoomAnchor = viewportPoint;
        ZoomLevel = targetZoom;
    }

    public bool HandleMouseWheelZoomOrScroll(int delta, Point viewportPoint, bool isControlPressed)
    {
        if (delta == 0)
        {
            return false;
        }

        if (isControlPressed)
        {
            var factor = delta > 0 ? ZoomInFactor : ZoomOutFactor;
            ZoomAtPoint(factor, viewportPoint);
            return true;
        }

        if (CurrentViewMode == ViewMode.Separated)
        {
            if (delta > 0 && TargetVerticalOffset <= 0.5 && PreviousPageCommand?.CanExecute(null) == true)
            {
                PreviousPageCommand.Execute(null);
                return true;
            }

            if (delta < 0 && TargetVerticalOffset >= ScrollableHeight - 0.5 && NextPageCommand?.CanExecute(null) == true)
            {
                NextPageCommand.Execute(null);
                return true;
            }
        }

        return false;
    }

    public void BeginPan(Point viewportPoint)
    {
        _isPanning = true;
        _panStartPoint = viewportPoint;
        _panStartHorizontalOffset = TargetHorizontalOffset;
        _panStartVerticalOffset = TargetVerticalOffset;
        UpdateCursor();
    }

    public bool UpdatePan(Point currentViewportPoint)
    {
        if (!_isPanning)
        {
            return false;
        }

        var delta = currentViewportPoint - _panStartPoint;
        ApplyScrollOffsets(
            Math.Max(0.0, _panStartHorizontalOffset - delta.X),
            Math.Max(0.0, _panStartVerticalOffset - delta.Y));
        return true;
    }

    public void EndPan()
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        UpdateCursor();
    }

    public void ApplyScrollOffsets(double horizontalOffset, double verticalOffset)
    {
        TargetHorizontalOffset = horizontalOffset;
        TargetVerticalOffset = verticalOffset;
        ScrollToHorizontalOffset(horizontalOffset);
        ScrollToVerticalOffset(verticalOffset);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (newContent is FrameworkElement fe)
        {
            fe.LayoutTransform = _scaleTransform;
        }
    }

    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);
        TargetHorizontalOffset = HorizontalOffset;
        TargetVerticalOffset = VerticalOffset;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled || e.Delta == 0)
        {
            return;
        }

        var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (HandleMouseWheelZoomOrScroll(e.Delta, e.GetPosition(this), isCtrl))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseWheel(e);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var isMiddlePan = e.ChangedButton == MouseButton.Middle && e.MiddleButton == MouseButtonState.Pressed;
        var isPointerPan = ActiveToolMode == ToolMode.Pointer &&
                           e.ChangedButton == MouseButton.Left &&
                           e.LeftButton == MouseButtonState.Pressed &&
                           !IsInsideInteractiveElement(e.OriginalSource as DependencyObject);
        var isLeftPan = e.ChangedButton == MouseButton.Left &&
                        e.LeftButton == MouseButtonState.Pressed &&
                        (_isSpaceKeyDown || ActiveToolMode == ToolMode.Pan || isPointerPan);

        if (isMiddlePan || isLeftPan)
        {
            if (ActiveToolMode == ToolMode.Pointer)
            {
                DeselectActiveElements();
            }

            BeginPan(e.GetPosition(this));
            CaptureMouse();
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseDown(e);
    }

    private static bool IsInsideInteractiveElement(DependencyObject? obj)
    {
        while (obj != null)
        {
            if (obj is CanvasElementContainer or CanvasElementAdorner)
            {
                return true;
            }

            if (obj is Button or TextBox or System.Windows.Controls.Primitives.ScrollBar or System.Windows.Controls.Primitives.Thumb or Slider)
            {
                return true;
            }

            obj = VisualTreeHelper.GetParent(obj);
        }

        return false;
    }

    private void DeselectActiveElements()
    {
        if (DataContext is ViewModels.MainViewModel mainVm && mainVm.ActivePage?.Elements is not null)
        {
            foreach (var el in mainVm.ActivePage.Elements)
            {
                el.IsSelected = false;
                el.IsEditing = false;
            }
        }
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_isPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed && e.LeftButton != MouseButtonState.Pressed)
            {
                EndPan();
            }
            else
            {
                UpdatePan(e.GetPosition(this));
                e.Handled = true;
                return;
            }
        }

        base.OnPreviewMouseMove(e);
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
        {
            EndPan();
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_isPanning)
        {
            EndPan();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key == Key.Space && !e.IsRepeat && e.OriginalSource is not TextBox)
        {
            _isSpaceKeyDown = true;
            UpdateCursor();
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key == Key.Space)
        {
            _isSpaceKeyDown = false;
            UpdateCursor();
        }

        base.OnPreviewKeyUp(e);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        _isSpaceKeyDown = false;
        UpdateCursor();
    }

    protected override void OnPreviewStylusDown(StylusDownEventArgs e)
    {
        base.OnPreviewStylusDown(e);
        if (e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Stylus)
        {
            _isStylusActive = true;
        }
    }

    protected override void OnPreviewStylusUp(StylusEventArgs e)
    {
        base.OnPreviewStylusUp(e);
        _isStylusActive = false;
    }

    protected override void OnStylusLeave(StylusEventArgs e)
    {
        base.OnStylusLeave(e);
        _isStylusActive = false;
    }

    protected override void OnManipulationStarting(ManipulationStartingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_isStylusActive)
        {
            e.Cancel();
            return;
        }

        e.ManipulationContainer = this;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
        e.Handled = true;
        base.OnManipulationStarting(e);
    }

    protected override void OnManipulationDelta(ManipulationDeltaEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_isStylusActive)
        {
            return;
        }

        var scaleDelta = (e.DeltaManipulation.Scale.X + e.DeltaManipulation.Scale.Y) / 2.0;
        if (double.IsFinite(scaleDelta) && Math.Abs(scaleDelta - 1.0) > 0.001)
        {
            ZoomAtPoint(scaleDelta, e.ManipulationOrigin);
        }

        var trans = e.DeltaManipulation.Translation;
        if (double.IsFinite(trans.X) && double.IsFinite(trans.Y) && (Math.Abs(trans.X) > 0.001 || Math.Abs(trans.Y) > 0.001))
        {
            ApplyScrollOffsets(
                Math.Max(0.0, TargetHorizontalOffset - trans.X),
                Math.Max(0.0, TargetVerticalOffset - trans.Y));
        }

        e.Handled = true;
        base.OnManipulationDelta(e);
    }

    private void UpdateCursor()
    {
        if (_isPanning)
        {
            Cursor = Cursors.ScrollAll;
        }
        else if (_isSpaceKeyDown || ActiveToolMode == ToolMode.Pan)
        {
            Cursor = Cursors.Hand;
        }
        else
        {
            Cursor = null;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Matches WPF CoerceValueCallback delegate signature")]
    private static object CoerceZoomLevel(DependencyObject d, object baseValue)
    {
        if (baseValue is double val && double.IsFinite(val))
        {
            return Math.Clamp(val, MinZoom, MaxZoom);
        }

        return DefaultZoom;
    }

    private static void OnZoomLevelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ZoomScrollViewer viewer)
        {
            return;
        }

        var oldZoom = (double)e.OldValue;
        var newZoom = (double)e.NewValue;

        viewer._scaleTransform.ScaleX = newZoom;
        viewer._scaleTransform.ScaleY = newZoom;

        if (viewer.Content is FrameworkElement fe && !ReferenceEquals(fe.LayoutTransform, viewer._scaleTransform))
        {
            fe.LayoutTransform = viewer._scaleTransform;
        }

        var anchor = viewer._pendingZoomAnchor ?? new Point(
            viewer.ViewportWidth > 0 ? viewer.ViewportWidth / 2.0 : 0.0,
            viewer.ViewportHeight > 0 ? viewer.ViewportHeight / 2.0 : 0.0);
        viewer._pendingZoomAnchor = null;

        if (oldZoom > 0.0 && double.IsFinite(oldZoom) && double.IsFinite(newZoom))
        {
            var ratio = newZoom / oldZoom;
            var targetH = Math.Max(0.0, ((viewer.TargetHorizontalOffset + anchor.X) * ratio) - anchor.X);
            var targetV = Math.Max(0.0, ((viewer.TargetVerticalOffset + anchor.Y) * ratio) - anchor.Y);
            viewer.ApplyScrollOffsets(targetH, targetV);
        }
    }

    private static void OnActiveToolModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomScrollViewer viewer)
        {
            viewer.UpdateCursor();
        }
    }
}
