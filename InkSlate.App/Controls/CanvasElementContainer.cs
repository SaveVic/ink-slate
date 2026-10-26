using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Serialization;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.Controls;

public sealed class CanvasElementContainer : ContentControl
{
    private CanvasElementAdorner? _adorner;
    private bool _isDragging;
    private bool _isPropertyChangedSubscribed;
    private Point _dragStartMouse;
    private Rect _dragInitialBounds;

    public CanvasElementContainer(
        CanvasElementViewModel element,
        UndoRedoManager? undoRedo = null,
        Guid pageId = default)
    {
        ArgumentNullException.ThrowIfNull(element);

        Element = element;
        UndoRedo = undoRedo;
        PageId = pageId;

        DataContext = element;
        Content = element;
        Focusable = false;
        IsTabStop = false;
        ClipToBounds = false;
        SnapsToDevicePixels = true;

        ApplyGeometry();
        SubscribePropertyChanged();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public CanvasElementViewModel Element { get; }
    public UndoRedoManager? UndoRedo { get; set; }
    public Guid PageId { get; set; }
    public bool IsPropertyChangedSubscribed => _isPropertyChangedSubscribed;

    public void ApplyGeometry()
    {
        var x = ClampFinite(Element.X, DocumentValidator.MinElementCoordinateDip, DocumentValidator.MaxElementCoordinateDip, 0.0);
        var y = ClampFinite(Element.Y, DocumentValidator.MinElementCoordinateDip, DocumentValidator.MaxElementCoordinateDip, 0.0);
        var width = ClampFinite(Element.Width, DocumentValidator.MinElementDimensionDip, DocumentValidator.MaxElementDimensionDip, 200.0);
        var height = ClampFinite(Element.Height, DocumentValidator.MinElementDimensionDip, DocumentValidator.MaxElementDimensionDip, 100.0);

        InkCanvas.SetLeft(this, x);
        InkCanvas.SetTop(this, y);
        Width = width;
        Height = height;
        Panel.SetZIndex(this, Element.ZIndex);

        _adorner?.InvalidateVisual();
    }

    private void SubscribePropertyChanged()
    {
        if (!_isPropertyChangedSubscribed)
        {
            Element.PropertyChanged += OnElementPropertyChanged;
            _isPropertyChangedSubscribed = true;
        }
    }

    private void UnsubscribePropertyChanged()
    {
        if (_isPropertyChangedSubscribed)
        {
            Element.PropertyChanged -= OnElementPropertyChanged;
            _isPropertyChangedSubscribed = false;
        }
    }

    public void Detach()
    {
        UnsubscribePropertyChanged();
        DetachAdorner();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribePropertyChanged();
        if (Element.IsSelected)
        {
            AttachAdorner();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribePropertyChanged();
        DetachAdorner();
    }

    private void AttachAdorner()
    {
        if (_adorner is not null)
        {
            return;
        }

        if (FindPageCanvasControl()?.ActiveToolMode == ToolMode.Lasso)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is not null)
        {
            _adorner = new CanvasElementAdorner(this, Element, UndoRedo, PageId);
            layer.Add(_adorner);
        }
    }

    public void RefreshAdornerAttachment()
    {
        if (Element.IsSelected && FindPageCanvasControl()?.ActiveToolMode is not ToolMode.Lasso)
        {
            AttachAdorner();
        }
        else
        {
            DetachAdorner();
        }
    }

    private PageCanvasControl? FindPageCanvasControl()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is PageCanvasControl pcc)
            {
                return pcc;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void DetachAdorner()
    {
        if (_adorner is not null)
        {
            _adorner.Detach();
            var layer = AdornerLayer.GetAdornerLayer(this);
            layer?.Remove(_adorner);
            _adorner = null;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.OriginalSource is DependencyObject dep && (FindAncestor<Button>(dep) is not null || dep is TextBox || dep is Wpf.Ui.Controls.TextBox))
        {
            return;
        }

        if (FindPageCanvasControl()?.ActiveToolMode == ToolMode.Lasso)
        {
            return;
        }

        var pageCanvas = FindPageCanvasControl();
        pageCanvas?.SelectExclusive(Element);
        Element.IsSelected = true;

        if (e.ClickCount == 2 && Element is TextCanvasElementViewModel textVm)
        {
            textVm.IsEditing = true;
            e.Handled = true;
            return;
        }

        var parent = VisualParent as UIElement;
        if (parent is not null)
        {
            _isDragging = true;
            _dragStartMouse = e.GetPosition(parent);
            _dragInitialBounds = new Rect(Element.X, Element.Y, Element.Width, Element.Height);
            CaptureMouse();
            e.Handled = true;
        }
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var parent = VisualParent as UIElement;
            if (parent is not null)
            {
                var currentMouse = e.GetPosition(parent);
                var deltaX = currentMouse.X - _dragStartMouse.X;
                var deltaY = currentMouse.Y - _dragStartMouse.Y;

                Element.X = Math.Max(0.0, _dragInitialBounds.X + deltaX);
                Element.Y = Math.Max(0.0, _dragInitialBounds.Y + deltaY);
                e.Handled = true;
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            var newBounds = new Rect(Element.X, Element.Y, Element.Width, Element.Height);
            if (newBounds != _dragInitialBounds)
            {
                if (UndoRedo is not null && !UndoRedo.IsReplaying)
                {
                    UndoRedo.Record(new ElementTransformedOperation(PageId, Element.Model, _dragInitialBounds, newBounds));
                }
            }

            e.Handled = true;
        }
    }

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanvasElementViewModel.X) or
            nameof(CanvasElementViewModel.Y) or
            nameof(CanvasElementViewModel.Width) or
            nameof(CanvasElementViewModel.Height) or
            nameof(CanvasElementViewModel.ZIndex) or
            nameof(CanvasElementViewModel.Bounds))
        {
            ApplyGeometry();
        }
        else if (e.PropertyName == nameof(CanvasElementViewModel.IsSelected))
        {
            if (Element.IsSelected)
            {
                AttachAdorner();
            }
            else
            {
                DetachAdorner();
            }
        }
    }

    private static double ClampFinite(double value, double min, double max, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}
