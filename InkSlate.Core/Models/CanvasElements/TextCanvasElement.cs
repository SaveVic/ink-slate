namespace InkSlate.Core.Models.CanvasElements;

public sealed class TextCanvasElement : CanvasElement
{
    public const string DefaultFontFamily = "Segoe UI";
    public const double DefaultFontSize = 16.0;
    public const string DefaultForegroundHex = "#FF1E1E1E";

    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = DefaultFontFamily;
    public double FontSize { get; set; } = DefaultFontSize;
    public string ForegroundHex { get; set; } = DefaultForegroundHex;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
}

