using System.Globalization;
using System.Windows.Ink;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models;

namespace InkSlate.App.ViewModels;

public sealed record ColorSwatchOption(string Hex, SolidColorBrush FrozenBrush);
public sealed record ThicknessOption(double ThicknessDip, double DotDiameter);
public sealed record EraserSizeOption(double SizeDip, double DotDiameter);

public sealed partial class ToolbarViewModel : ObservableObject
{
    public const double DefaultPenThickness = 2.5;
    public const double HighlighterWidth = 12.0;
    public const double HighlighterHeight = 24.0;
    public const byte HighlighterAlpha = 0x66;

    public const string DefaultPenColorHex = "#FF1E1E1E";
    public const string DefaultHighlighterColorHex = "#66FFEB3B";

    public const double DefaultEraserSize = 16.0;

    public static IReadOnlyList<double> PenThicknessPresets { get; } = [1.5, 2.5, 4.0, 6.5];
    public static IReadOnlyList<double> EraserSizePresets { get; } = [8.0, 16.0, 32.0];

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStrokeEraserMode))]
    [NotifyPropertyChangedFor(nameof(IsPointEraserMode))]
    [NotifyPropertyChangedFor(nameof(IsObjectEraserMode))]
    private EraserMode _selectedEraserMode = EraserMode.Stroke;

    [ObservableProperty]
    private double _selectedEraserSize = DefaultEraserSize;

    [ObservableProperty]
    private bool _isEraserOptionsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLassoStrokesOnly))]
    [NotifyPropertyChangedFor(nameof(IsLassoElementsOnly))]
    [NotifyPropertyChangedFor(nameof(IsLassoAllObjects))]
    private LassoFilterMode _selectedLassoFilterMode = LassoFilterMode.AllObjects;

    [ObservableProperty]
    private bool _isLassoOptionsOpen;

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
        EraserSizeOptions =
        [
            new EraserSizeOption(8.0, 7.0),
            new EraserSizeOption(16.0, 11.0),
            new EraserSizeOption(32.0, 16.0)
        ];

        CurrentDrawingAttributes = new DrawingAttributes();
        ApplyAttributesToSharedInstance();
    }

    public DrawingAttributes CurrentDrawingAttributes { get; }
    public IReadOnlyList<ColorSwatchOption> ColorSwatches { get; }
    public IReadOnlyList<ThicknessOption> ThicknessOptions { get; }
    public IReadOnlyList<EraserSizeOption> EraserSizeOptions { get; }

    public bool IsPenToolActive => ActiveTool == ToolMode.Pen;
    public bool IsHighlighterToolActive => ActiveTool == ToolMode.Highlighter;
    public bool IsEraserToolActive => ActiveTool == ToolMode.Eraser;
    public bool IsLassoToolActive => ActiveTool == ToolMode.Lasso;
    public bool IsTextToolActive => ActiveTool == ToolMode.Text;
    public bool IsImageToolActive => ActiveTool == ToolMode.Image;

    public bool IsStrokeEraserMode => SelectedEraserMode == EraserMode.Stroke;
    public bool IsPointEraserMode => SelectedEraserMode == EraserMode.Point;
    public bool IsObjectEraserMode => SelectedEraserMode == EraserMode.Object;

    public bool IsLassoStrokesOnly => SelectedLassoFilterMode == LassoFilterMode.StrokesOnly;
    public bool IsLassoElementsOnly => SelectedLassoFilterMode == LassoFilterMode.ElementsOnly;
    public bool IsLassoAllObjects => SelectedLassoFilterMode == LassoFilterMode.AllObjects;

    [RelayCommand]
    private void SelectTool(ToolMode mode)
    {
        if (ActiveTool == mode)
        {
            if (mode is ToolMode.Pen or ToolMode.Highlighter)
            {
                IsEraserOptionsOpen = false;
                IsLassoOptionsOpen = false;
                IsPenOptionsOpen = !IsPenOptionsOpen;
                return;
            }

            if (mode == ToolMode.Eraser)
            {
                IsPenOptionsOpen = false;
                IsLassoOptionsOpen = false;
                IsEraserOptionsOpen = !IsEraserOptionsOpen;
                return;
            }

            if (mode == ToolMode.Lasso)
            {
                IsPenOptionsOpen = false;
                IsEraserOptionsOpen = false;
                IsLassoOptionsOpen = !IsLassoOptionsOpen;
                return;
            }
        }

        IsPenOptionsOpen = false;
        IsEraserOptionsOpen = false;
        IsLassoOptionsOpen = false;
        ActiveTool = mode;
        ApplyAttributesToSharedInstance();
    }

    [RelayCommand]
    private void SelectLassoFilter(LassoFilterMode mode)
    {
        SelectedLassoFilterMode = mode;
        IsLassoOptionsOpen = false;
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

    [RelayCommand]
    private void SelectEraserMode(EraserMode mode)
    {
        SelectedEraserMode = mode;
        IsEraserOptionsOpen = false;
    }

    [RelayCommand]
    private void SelectEraserSize(double size)
    {
        if (!double.IsFinite(size) || size <= 0.0)
        {
            return;
        }

        SelectedEraserSize = Math.Clamp(size, 2.0, 128.0);
        SelectedEraserMode = EraserMode.Point;
        IsEraserOptionsOpen = false;
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
