using System.Globalization;
using System.Windows.Ink;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models;

namespace InkSlate.App.ViewModels;

public sealed record ColorSwatchOption(string Hex, SolidColorBrush FrozenBrush);
public sealed record ThicknessOption(double ThicknessDip, double DotDiameter);

public sealed partial class ToolbarViewModel : ObservableObject
{
    public const double DefaultPenThickness = 2.5;
    public const double HighlighterWidth = 12.0;
    public const double HighlighterHeight = 24.0;
    public const byte HighlighterAlpha = 0x66;

    public const string DefaultPenColorHex = "#FF1E1E1E";
    public const string DefaultHighlighterColorHex = "#66FFEB3B";

    public static IReadOnlyList<double> PenThicknessPresets { get; } = [1.5, 2.5, 4.0, 6.5];

    public static IReadOnlyList<string> CuratedSwatchHexes { get; } =
    [
        "#FF1E1E1E", // Charcoal Black
        "#FF2563EB", // Cobalt Blue
        "#FFDC2626", // Crimson Red
        "#FF16A34A", // Emerald Green
        "#FF9333EA", // Amethyst Purple
        "#FFFFEB3B"  // Highlighter Yellow
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPenToolActive))]
    [NotifyPropertyChangedFor(nameof(IsHighlighterToolActive))]
    [NotifyPropertyChangedFor(nameof(IsEraserToolActive))]
    [NotifyPropertyChangedFor(nameof(IsLassoToolActive))]
    [NotifyPropertyChangedFor(nameof(IsTextToolActive))]
    [NotifyPropertyChangedFor(nameof(IsImageToolActive))]
    private ToolMode _activeTool = ToolMode.Pen;

    [ObservableProperty]
    private double _selectedPenThickness = DefaultPenThickness;

    [ObservableProperty]
    private string _selectedPenColorHex = DefaultPenColorHex;

    [ObservableProperty]
    private string _selectedHighlighterColorHex = DefaultHighlighterColorHex;

    [ObservableProperty]
    private bool _isPenOptionsOpen;

    public ToolbarViewModel()
    {
        ColorSwatches = CreateFrozenSwatches();
        ThicknessOptions =
        [
            new ThicknessOption(1.5, 6.0),
            new ThicknessOption(2.5, 9.0),
            new ThicknessOption(4.0, 12.0),
            new ThicknessOption(6.5, 16.0)
        ];

        CurrentDrawingAttributes = new DrawingAttributes();
        ApplyAttributesToSharedInstance();
    }

    public DrawingAttributes CurrentDrawingAttributes { get; }
    public IReadOnlyList<ColorSwatchOption> ColorSwatches { get; }
    public IReadOnlyList<ThicknessOption> ThicknessOptions { get; }

    public bool IsPenToolActive => ActiveTool == ToolMode.Pen;
    public bool IsHighlighterToolActive => ActiveTool == ToolMode.Highlighter;
    public bool IsEraserToolActive => ActiveTool == ToolMode.Eraser;
    public bool IsLassoToolActive => ActiveTool == ToolMode.Lasso;
    public bool IsTextToolActive => ActiveTool == ToolMode.Text;
    public bool IsImageToolActive => ActiveTool == ToolMode.Image;

    [RelayCommand]
    private void SelectTool(ToolMode mode)
    {
        if (ActiveTool == mode && mode is ToolMode.Pen or ToolMode.Highlighter)
        {
            IsPenOptionsOpen = !IsPenOptionsOpen;
            return;
        }

        IsPenOptionsOpen = false;
        ActiveTool = mode;
        ApplyAttributesToSharedInstance();
    }

    [RelayCommand]
    private void SelectColorSwatch(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || !TryParseRgb(hex, out var r, out var g, out var b))
        {
            return;
        }

        if (ActiveTool == ToolMode.Highlighter)
        {
            SelectedHighlighterColorHex = string.Create(CultureInfo.InvariantCulture, $"#{HighlighterAlpha:X2}{r:X2}{g:X2}{b:X2}");
        }
        else
        {
            SelectedPenColorHex = string.Create(CultureInfo.InvariantCulture, $"#FF{r:X2}{g:X2}{b:X2}");
        }

        ApplyAttributesToSharedInstance();
        IsPenOptionsOpen = false;
    }

    [RelayCommand]
    private void SelectPenThickness(double thickness)
    {
        if (!double.IsFinite(thickness) || thickness <= 0.0)
        {
            return;
        }

        SelectedPenThickness = Math.Clamp(thickness, 0.5, 32.0);
        ApplyAttributesToSharedInstance();
        IsPenOptionsOpen = false;
    }

    private void ApplyAttributesToSharedInstance()
    {
        if (ActiveTool == ToolMode.Highlighter)
        {
            CurrentDrawingAttributes.FitToCurve = false;
            CurrentDrawingAttributes.IgnorePressure = true;
            CurrentDrawingAttributes.IsHighlighter = true;
            CurrentDrawingAttributes.StylusTip = StylusTip.Rectangle;
            CurrentDrawingAttributes.Width = HighlighterWidth;
            CurrentDrawingAttributes.Height = HighlighterHeight;
            CurrentDrawingAttributes.Color = ParseColor(SelectedHighlighterColorHex, HighlighterAlpha);
        }
        else
        {
            CurrentDrawingAttributes.FitToCurve = true;
            CurrentDrawingAttributes.IgnorePressure = false;
            CurrentDrawingAttributes.IsHighlighter = false;
            CurrentDrawingAttributes.StylusTip = StylusTip.Ellipse;
            CurrentDrawingAttributes.Width = SelectedPenThickness;
            CurrentDrawingAttributes.Height = SelectedPenThickness;
            CurrentDrawingAttributes.Color = ParseColor(SelectedPenColorHex, 0xFF);
        }
    }

    private static ColorSwatchOption[] CreateFrozenSwatches()
    {
        var result = new List<ColorSwatchOption>(CuratedSwatchHexes.Count);
        foreach (var hex in CuratedSwatchHexes)
        {
            if (TryParseRgb(hex, out var r, out var g, out var b))
            {
                var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                brush.Freeze();
                result.Add(new ColorSwatchOption(hex, brush));
            }
        }
        return [.. result];
    }

    private static Color ParseColor(string hex, byte defaultAlpha)
    {
        if (TryParseRgb(hex, out var r, out var g, out var b))
        {
            var a = hex.Length == 9 && byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedA)
                ? parsedA
                : defaultAlpha;
            return Color.FromArgb(a, r, g, b);
        }

        return Color.FromArgb(defaultAlpha, 0x1E, 0x1E, 0x1E);
    }

    private static bool TryParseRgb(string hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex) || !hex.StartsWith('#'))
        {
            return false;
        }

        var offset = hex.Length == 9 ? 3 : 1;
        if (hex.Length != 7 && hex.Length != 9)
        {
            return false;
        }

        if (byte.TryParse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
            byte.TryParse(hex.AsSpan(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
            byte.TryParse(hex.AsSpan(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
        {
            return true;
        }

        return false;
    }
}
