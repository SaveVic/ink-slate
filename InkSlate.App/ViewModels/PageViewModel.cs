using CommunityToolkit.Mvvm.ComponentModel;
using InkSlate.Core.Models;

namespace InkSlate.App.ViewModels;

public sealed partial class PageViewModel : ObservableObject
{
    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private string _backgroundHex;

    [ObservableProperty]
    private bool _isActive;

    public PageViewModel(NotePage model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        _pageNumber = model.PageNumber;
        _width = model.Width;
        _height = model.Height;
        _backgroundHex = model.BackgroundHex;
    }

    public NotePage Model { get; }
    public Guid Id => Model.Id;

    partial void OnPageNumberChanged(int value) => Model.PageNumber = value;
    partial void OnWidthChanged(double value) => Model.Width = value;
    partial void OnHeightChanged(double value) => Model.Height = value;
    partial void OnBackgroundHexChanged(string value) => Model.BackgroundHex = value;
}
