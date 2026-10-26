using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Services;

namespace InkSlate.App.ViewModels;

public sealed partial class ImageCanvasElementViewModel : CanvasElementViewModel
{
    private double _cropX;
    private double _cropY;
    private double _cropWidth;
    private double _cropHeight;
    private BitmapSource? _imageSource;

    public ImageCanvasElementViewModel(ImageCanvasElement model, Func<string, byte[]?>? assetResolver = null)
        : base(model)
    {
        _cropX = model.CropX;
        _cropY = model.CropY;
        _cropWidth = model.CropWidth;
        _cropHeight = model.CropHeight;

        ResolveImage(assetResolver);
    }

    public ImageCanvasElement ImageModel => (ImageCanvasElement)Model;

    public string AssetKey => ImageModel.AssetKey;

    public double CropX
    {
        get => _cropX;
        set
        {
            if (SetProperty(ref _cropX, value))
            {
                ImageModel.CropX = value;
                OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(ClipGeometry));
                OnPropertyChanged(nameof(ClipGeometry));
            }
        }
    }

    public double CropY
    {
        get => _cropY;
        set
        {
            if (SetProperty(ref _cropY, value))
            {
                ImageModel.CropY = value;
                OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(ClipGeometry));
                OnPropertyChanged(nameof(ClipGeometry));
            }
        }
    }

    public double CropWidth
    {
        get => _cropWidth;
        set
        {
            if (SetProperty(ref _cropWidth, value))
            {
                ImageModel.CropWidth = value;
                OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(ClipGeometry));
                OnPropertyChanged(nameof(ClipGeometry));
            }
        }
    }

    public double CropHeight
    {
        get => _cropHeight;
        set
        {
            if (SetProperty(ref _cropHeight, value))
            {
                ImageModel.CropHeight = value;
                OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(ClipGeometry));
                OnPropertyChanged(nameof(ClipGeometry));
            }
        }
    }

    public Rect? CropRect =>
        CropWidth > 0 && CropHeight > 0
            ? new Rect(CropX, CropY, CropWidth, CropHeight)
            : null;

    public Geometry? ClipGeometry =>
        CropWidth > 0 && CropHeight > 0
            ? new RectangleGeometry(new Rect(CropX, CropY, CropWidth, CropHeight))
            : null;

    public BitmapSource? ImageSource
    {
        get => _imageSource;
        set => SetProperty(ref _imageSource, value);
    }

    public void ResolveImage(Func<string, byte[]?>? assetResolver)
    {
        if (ImageSource is not null || assetResolver is null || string.IsNullOrWhiteSpace(AssetKey))
        {
            return;
        }

        try
        {
            var bytes = assetResolver(AssetKey);
            if (bytes is { Length: > 0 })
            {
                var result = MediaIngestionService.IngestBytes(bytes, AssetKey);
                ImageSource = result.FrozenBitmap;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Corrupted or missing asset fallback
            ImageSource = null;
        }
    }

    public override void RefreshFromModel()
    {
        base.RefreshFromModel();
        CropX = ImageModel.CropX;
        CropY = ImageModel.CropY;
        CropWidth = ImageModel.CropWidth;
        CropHeight = ImageModel.CropHeight;
        OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(ClipGeometry));
    }
}
