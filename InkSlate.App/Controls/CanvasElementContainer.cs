using System.ComponentModel;
using System.Windows.Controls;
using InkSlate.App.ViewModels;
using InkSlate.Core.Serialization;

namespace InkSlate.App.Controls;

public sealed class CanvasElementContainer : ContentControl
{
    public CanvasElementContainer(CanvasElementViewModel element)
    {
        ArgumentNullException.ThrowIfNull(element);

        Element = element;
        DataContext = element;
        Content = element.Model;
        Focusable = false;
        IsTabStop = false;
        ClipToBounds = true;
        SnapsToDevicePixels = true;

        ApplyGeometry();
        Element.PropertyChanged += OnElementPropertyChanged;
    }

    public CanvasElementViewModel Element { get; }

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
    }

    public void Detach()
    {
        Element.PropertyChanged -= OnElementPropertyChanged;
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

