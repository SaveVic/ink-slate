using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.App.ViewModels;

public sealed partial class TextCanvasElementViewModel : CanvasElementViewModel
{
    private string _text;
    private string _fontFamily;
    private double _fontSize;
    private string _foregroundHex;
    private bool _isBold;
    private bool _isItalic;

    public TextCanvasElementViewModel(TextCanvasElement model)
        : base(model)
    {
        _text = model.Text;
        _fontFamily = model.FontFamily;
        _fontSize = model.FontSize;
        _foregroundHex = model.ForegroundHex;
        _isBold = model.IsBold;
        _isItalic = model.IsItalic;
    }

    public TextCanvasElement TextModel => (TextCanvasElement)Model;

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                TextModel.Text = value;
            }
        }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            if (SetProperty(ref _fontFamily, value))
            {
                TextModel.FontFamily = value;
            }
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            var clamped = Math.Clamp(value, 8.0, 144.0);
            if (SetProperty(ref _fontSize, clamped))
            {
                TextModel.FontSize = clamped;
            }
        }
    }

    public string ForegroundHex
    {
        get => _foregroundHex;
        set
        {
            if (SetProperty(ref _foregroundHex, value))
            {
                TextModel.ForegroundHex = value;
            }
        }
    }

    public bool IsBold
    {
        get => _isBold;
        set
        {
            if (SetProperty(ref _isBold, value))
            {
                TextModel.IsBold = value;
            }
        }
    }

    public bool IsItalic
    {
        get => _isItalic;
        set
        {
            if (SetProperty(ref _isItalic, value))
            {
                TextModel.IsItalic = value;
            }
        }
    }

    [RelayCommand]
    public void IncreaseFontSize() => FontSize = Math.Min(FontSize + 2.0, 144.0);

    [RelayCommand]
    public void DecreaseFontSize() => FontSize = Math.Max(FontSize - 2.0, 8.0);

    [RelayCommand]
    public void ToggleBold() => IsBold = !IsBold;

    [RelayCommand]
    public void ToggleItalic() => IsItalic = !IsItalic;

    [RelayCommand]
    public void SetForegroundHex(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            ForegroundHex = hex;
        }
    }

    public override void RefreshFromModel()
    {
        base.RefreshFromModel();
        Text = TextModel.Text;
        FontFamily = TextModel.FontFamily;
        FontSize = TextModel.FontSize;
        ForegroundHex = TextModel.ForegroundHex;
        IsBold = TextModel.IsBold;
        IsItalic = TextModel.IsItalic;
    }
}

