using System.Globalization;
using System.Windows.Ink;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models;

namespace InkSlate.App.ViewModels;

public sealed partial class ColorSwatchOption : ObservableObject
{
    public ColorSwatchOption(string hex, SolidColorBrush frozenBrush)
    {
        Hex = hex;
        FrozenBrush = frozenBrush;
    }

    public string Hex { get; }
    public SolidColorBrush FrozenBrush { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class ThicknessOption : ObservableObject
{
    public ThicknessOption(double thicknessDip, double dotDiameter)
    {
        ThicknessDip = thicknessDip;
        DotDiameter = dotDiameter;
    }

    public double ThicknessDip { get; }
    public double DotDiameter { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class EraserSizeOption : ObservableObject
{
    public EraserSizeOption(double sizeDip, double dotDiameter)
    {
        SizeDip = sizeDip;
        DotDiameter = dotDiameter;
    }

    public double SizeDip { get; }
    public double DotDiameter { get; }

    [ObservableProperty]
    private bool _isSelected;
}

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
    [NotifyPropertyChangedFor(nameof(IsPointerToolActive))]
    [NotifyPropertyChangedFor(nameof(IsPenToolActive))]
    [NotifyPropertyChangedFor(nameof(IsHighlighterToolActive))]
    [NotifyPropertyChangedFor(nameof(IsEraserToolActive))]
    [NotifyPropertyChangedFor(nameof(IsLassoToolActive))]
    [NotifyPropertyChangedFor(nameof(IsTextToolActive))]
    [NotifyPropertyChangedFor(nameof(IsImageToolActive))]
    private ToolMode _activeTool = ToolMode.Pointer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTopDocked))]
    [NotifyPropertyChangedFor(nameof(IsLeftDocked))]
    [NotifyPropertyChangedFor(nameof(IsTopFloating))]
    private ToolbarPlacement _placement = ToolbarPlacement.TopDocked;

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

    public const double DefaultTextFontSize = 18.0;
    public const string DefaultTextColorHex = "#FF1E1E1E";

    [ObservableProperty]
    private double _selectedTextFontSize = DefaultTextFontSize;

    [ObservableProperty]
    private bool _isTextBold;

    [ObservableProperty]
    private bool _isTextItalic;

    [ObservableProperty]
    private string _selectedTextColorHex = DefaultTextColorHex;

    [ObservableProperty]
    private bool _isTextOptionsOpen;

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
        TextColorSwatches = CreateFrozenSwatches();
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
        UpdateSubtoolSelectionStates();
    }

    public DrawingAttributes CurrentDrawingAttributes { get; }
    public IReadOnlyList<ColorSwatchOption> ColorSwatches { get; }
    public IReadOnlyList<ColorSwatchOption> TextColorSwatches { get; }
    public IReadOnlyList<ThicknessOption> ThicknessOptions { get; }
    public IReadOnlyList<EraserSizeOption> EraserSizeOptions { get; }

    public bool IsPointerToolActive => ActiveTool == ToolMode.Pointer;
    public bool IsPenToolActive => ActiveTool == ToolMode.Pen;
    public bool IsHighlighterToolActive => ActiveTool == ToolMode.Highlighter;
    public bool IsEraserToolActive => ActiveTool == ToolMode.Eraser;
    public bool IsLassoToolActive => ActiveTool == ToolMode.Lasso;
    public bool IsTextToolActive => ActiveTool == ToolMode.Text;
    public bool IsImageToolActive => ActiveTool == ToolMode.Image;

    public bool IsTopDocked => Placement == ToolbarPlacement.TopDocked;
    public bool IsLeftDocked => Placement == ToolbarPlacement.LeftDocked;
    public bool IsTopFloating => Placement == ToolbarPlacement.TopFloating;

    public bool IsStrokeEraserMode => SelectedEraserMode == EraserMode.Stroke;
    public bool IsPointEraserMode => SelectedEraserMode == EraserMode.Point;
    public bool IsObjectEraserMode => SelectedEraserMode == EraserMode.Object;

    public bool IsLassoStrokesOnly => SelectedLassoFilterMode == LassoFilterMode.StrokesOnly;
    public bool IsLassoElementsOnly => SelectedLassoFilterMode == LassoFilterMode.ElementsOnly;
    public bool IsLassoAllObjects => SelectedLassoFilterMode == LassoFilterMode.AllObjects;

    [RelayCommand]
    private void CyclePlacement()
    {
        Placement = Placement switch
        {
            ToolbarPlacement.TopDocked => ToolbarPlacement.LeftDocked,
            ToolbarPlacement.LeftDocked => ToolbarPlacement.TopFloating,
            ToolbarPlacement.TopFloating => ToolbarPlacement.TopDocked,
            _ => ToolbarPlacement.TopDocked
        };
    }

    [RelayCommand]
    private void SetPlacement(ToolbarPlacement placement)
    {
        Placement = placement;
    }

    partial void OnActiveToolChanged(ToolMode value) => UpdateSubtoolSelectionStates();
    partial void OnSelectedPenThicknessChanged(double value) => UpdateSubtoolSelectionStates();
    partial void OnSelectedPenColorHexChanged(string value) => UpdateSubtoolSelectionStates();
    partial void OnSelectedHighlighterColorHexChanged(string value) => UpdateSubtoolSelectionStates();
    partial void OnSelectedTextColorHexChanged(string value) => UpdateSubtoolSelectionStates();
    partial void OnSelectedEraserSizeChanged(double value) => UpdateSubtoolSelectionStates();
    partial void OnSelectedEraserModeChanged(EraserMode value) => UpdateSubtoolSelectionStates();
    partial void OnSelectedLassoFilterModeChanged(LassoFilterMode value) => UpdateSubtoolSelectionStates();

    [RelayCommand]
    public void ActivateOrCycleEraser()
    {
        IsPenOptionsOpen = false;
        IsEraserOptionsOpen = false;
        IsLassoOptionsOpen = false;
        IsTextOptionsOpen = false;

        if (ActiveTool != ToolMode.Eraser)
        {
            ActiveTool = ToolMode.Eraser;
            ApplyAttributesToSharedInstance();
            UpdateSubtoolSelectionStates();
            return;
        }

        SelectedEraserMode = SelectedEraserMode switch
        {
            EraserMode.Stroke => EraserMode.Point,
            EraserMode.Point => EraserMode.Object,
            EraserMode.Object => EraserMode.Stroke,
            _ => EraserMode.Stroke
        };
    }

    [RelayCommand]
    public void ActivateOrCycleLasso()
    {
        IsPenOptionsOpen = false;
        IsEraserOptionsOpen = false;
        IsLassoOptionsOpen = false;
        IsTextOptionsOpen = false;

        if (ActiveTool != ToolMode.Lasso)
        {
            ActiveTool = ToolMode.Lasso;
            ApplyAttributesToSharedInstance();
            UpdateSubtoolSelectionStates();
            return;
        }

        SelectedLassoFilterMode = SelectedLassoFilterMode switch
        {
            LassoFilterMode.AllObjects => LassoFilterMode.StrokesOnly,
            LassoFilterMode.StrokesOnly => LassoFilterMode.ElementsOnly,
            LassoFilterMode.ElementsOnly => LassoFilterMode.AllObjects,
            _ => LassoFilterMode.AllObjects
        };
    }

    [RelayCommand]
    private void SelectTool(ToolMode mode)
    {
        if (ActiveTool == mode)
        {
            if (mode is ToolMode.Pen or ToolMode.Highlighter)
            {
                IsEraserOptionsOpen = false;
                IsLassoOptionsOpen = false;
                IsTextOptionsOpen = false;
                IsPenOptionsOpen = !IsPenOptionsOpen;
                return;
            }

            if (mode == ToolMode.Eraser)
            {
                IsPenOptionsOpen = false;
                IsLassoOptionsOpen = false;
                IsTextOptionsOpen = false;
                IsEraserOptionsOpen = !IsEraserOptionsOpen;
                return;
            }

            if (mode == ToolMode.Lasso)
            {
                IsPenOptionsOpen = false;
                IsEraserOptionsOpen = false;
                IsTextOptionsOpen = false;
                IsLassoOptionsOpen = !IsLassoOptionsOpen;
                return;
            }

            if (mode == ToolMode.Text)
            {
                IsPenOptionsOpen = false;
                IsEraserOptionsOpen = false;
                IsLassoOptionsOpen = false;
                IsTextOptionsOpen = !IsTextOptionsOpen;
                return;
            }
        }

        IsPenOptionsOpen = false;
        IsEraserOptionsOpen = false;
        IsLassoOptionsOpen = false;
        IsTextOptionsOpen = false;
        ActiveTool = mode;
        ApplyAttributesToSharedInstance();
        UpdateSubtoolSelectionStates();
    }

    [RelayCommand]
    private void IncreaseTextFontSize()
    {
        SelectedTextFontSize = Math.Clamp(SelectedTextFontSize + 2.0, 8.0, 96.0);
    }

    [RelayCommand]
    private void DecreaseTextFontSize()
    {
        SelectedTextFontSize = Math.Clamp(SelectedTextFontSize - 2.0, 8.0, 96.0);
    }

    [RelayCommand]
    private void ToggleTextBold()
    {
        IsTextBold = !IsTextBold;
    }

    [RelayCommand]
    private void ToggleTextItalic()
    {
        IsTextItalic = !IsTextItalic;
    }

    [RelayCommand]
    private void SelectTextColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        SelectedTextColorHex = hex;
    }

    public void UpdateSubtoolSelectionStates()
    {
        var activeHex = ActiveTool == ToolMode.Highlighter ? SelectedHighlighterColorHex : SelectedPenColorHex;
        foreach (var swatch in ColorSwatches)
        {
            swatch.IsSelected = string.Equals(swatch.Hex, activeHex, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var swatch in TextColorSwatches)
        {
            swatch.IsSelected = string.Equals(swatch.Hex, SelectedTextColorHex, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var opt in ThicknessOptions)
        {
            opt.IsSelected = Math.Abs(opt.ThicknessDip - SelectedPenThickness) < 0.05;
        }

        foreach (var eraser in EraserSizeOptions)
        {
            eraser.IsSelected = SelectedEraserMode == EraserMode.Point && Math.Abs(eraser.SizeDip - SelectedEraserSize) < 0.05;
        }
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
