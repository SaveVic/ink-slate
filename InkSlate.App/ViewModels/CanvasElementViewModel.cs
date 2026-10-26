using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.App.ViewModels;

public partial class CanvasElementViewModel : ObservableObject
{
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private int _zIndex;
    private bool _isSelected;
    private bool _isEditing;

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

    public Action<CanvasElementViewModel>? RequestDelete { get; set; }

    [RelayCommand]
    public void Delete()
    {
        RequestDelete?.Invoke(this);
    }

    public CanvasElement Model { get; }

    public Guid Id => Model.Id;

    public bool IsImage => Model is ImageCanvasElement;
    public bool IsText => Model is TextCanvasElement;

    public double X
    {
        get => _x;
        set
        {
            if (SetProperty(ref _x, value))
            {
                Model.X = value;
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (SetProperty(ref _y, value))
            {
                Model.Y = value;
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            if (SetProperty(ref _width, value))
            {
                Model.Width = value;
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (SetProperty(ref _height, value))
            {
                Model.Height = value;
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    public int ZIndex
    {
        get => _zIndex;
        set
        {
            if (SetProperty(ref _zIndex, value))
            {
                Model.ZIndex = value;
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public virtual bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public Rect Bounds => new(X, Y, Math.Max(0.0, Width), Math.Max(0.0, Height));

    public bool HitTest(Point point, double tolerance = 0.0)
    {
        var r = Math.Max(0.0, tolerance);
        return point.X >= X - r &&
               point.X <= X + Math.Max(0.0, Width) + r &&
               point.Y >= Y - r &&
               point.Y <= Y + Math.Max(0.0, Height) + r;
    }

    public virtual void RefreshFromModel()
    {
        X = Model.X;
        Y = Model.Y;
        Width = Model.Width;
        Height = Model.Height;
        ZIndex = Model.ZIndex;
    }

    public static CanvasElementViewModel Create(CanvasElement model, Func<string, byte[]?>? assetResolver = null) =>
        model switch
        {
            TextCanvasElement text => new TextCanvasElementViewModel(text),
            ImageCanvasElement image => new ImageCanvasElementViewModel(image, assetResolver),
            _ => new CanvasElementViewModel(model)
        };
}
