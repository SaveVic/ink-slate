using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.App.ViewModels;

public sealed partial class CanvasElementViewModel : ObservableObject
{
    public CanvasElementViewModel(CanvasElement model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        _x = model.X;
        _y = model.Y;
        _width = model.Width;
        _height = model.Height;
        _zIndex = model.ZIndex;
    }

    public CanvasElement Model { get; }

    public Guid Id => Model.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double _x;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double _y;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double _width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double _height;

    [ObservableProperty]
    private int _zIndex;

    public Rect Bounds => new(X, Y, Math.Max(0.0, Width), Math.Max(0.0, Height));

    public bool HitTest(Point point, double tolerance = 0.0)
    {
        var r = Math.Max(0.0, tolerance);
        return point.X >= X - r &&
               point.X <= X + Math.Max(0.0, Width) + r &&
               point.Y >= Y - r &&
               point.Y <= Y + Math.Max(0.0, Height) + r;
    }

    partial void OnXChanged(double value) => Model.X = value;
    partial void OnYChanged(double value) => Model.Y = value;
    partial void OnWidthChanged(double value) => Model.Width = value;
    partial void OnHeightChanged(double value) => Model.Height = value;
    partial void OnZIndexChanged(int value) => Model.ZIndex = value;
}

