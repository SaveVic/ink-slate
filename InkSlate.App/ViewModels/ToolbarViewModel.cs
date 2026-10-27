using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models;
using InkSlate.Core.Services;

namespace InkSlate.App.ViewModels;

public sealed partial class ColorSwatchOption : ObservableObject
{
    public ColorSwatchOption(string hex, SolidColorBrush frozenBrush, bool isCustom = false)
    {
        Hex = hex;
        FrozenBrush = frozenBrush;
        IsCustom = isCustom;
    }

    public string Hex { get; }
    public SolidColorBrush FrozenBrush { get; }
    public bool IsCustom { get; }

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

public sealed partial class CustomPenPresetViewModel : ObservableObject
{
    public CustomPenPresetViewModel(
        Guid id,
        string name,
        string colorHex,
        double thicknessDip,
        string shortcut,
        SolidColorBrush frozenBrush)
    {
        Id = id;
        _name = name;
        _colorHex = colorHex;
        _thicknessDip = thicknessDip;
        _shortcut = shortcut;
        _frozenBrush = frozenBrush;
        _dotDiameter = ComputeDotDiameter(thicknessDip);
    }

    public Guid Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipText))]
    private string _name;

    [ObservableProperty]
    private string _colorHex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipText))]
    private double _thicknessDip;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShortcut))]
    [NotifyPropertyChangedFor(nameof(TooltipText))]
    private string _shortcut;

    [ObservableProperty]
    private SolidColorBrush _frozenBrush;

    [ObservableProperty]
    private double _dotDiameter;

    [ObservableProperty]
    private bool _isSelected;

    public bool HasShortcut => !string.IsNullOrWhiteSpace(Shortcut);

    public string TooltipText => HasShortcut
        ? $"{Name} ({ThicknessDip:0.#}px) — Shortcut: {Shortcut}\nLeft-click to activate • Right-click to edit"
        : $"{Name} ({ThicknessDip:0.#}px)\nLeft-click to activate • Right-click to edit";

    public void UpdateFrom(string name, string colorHex, double thicknessDip, string shortcut, SolidColorBrush frozenBrush)
    {
        Name = name;
        ColorHex = colorHex;
        ThicknessDip = thicknessDip;
        Shortcut = shortcut;
        FrozenBrush = frozenBrush;
        DotDiameter = ComputeDotDiameter(thicknessDip);
    }

    public static double ComputeDotDiameter(double thicknessDip)
    {
        return Math.Clamp(thicknessDip * 2.0 + 3.0, 6.0, 22.0);
    }
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
    private bool _isCustomColorPickerOpen;

    [ObservableProperty]
    private byte _customColorR = 0x25;

    [ObservableProperty]
    private byte _customColorG = 0x63;

    [ObservableProperty]
    private byte _customColorB = 0xEB;

    [ObservableProperty]
    private double _customColorHue = 221.0;

    [ObservableProperty]
    private double _customColorSaturation = 0.84;

    [ObservableProperty]
    private double _customColorValue = 0.92;

    [ObservableProperty]
    private string _customColorHexInput = "#2563EB";

    [ObservableProperty]
    private SolidColorBrush _customColorPreviewBrush = null!;

    [ObservableProperty]
    private SolidColorBrush _customHueBaseBrush = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLassoStrokesOnly))]
    [NotifyPropertyChangedFor(nameof(IsLassoElementsOnly))]
    [NotifyPropertyChangedFor(nameof(IsLassoAllObjects))]
    private LassoFilterMode _selectedLassoFilterMode = LassoFilterMode.AllObjects;

    [ObservableProperty]
    private bool _isLassoOptionsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingExistingCustomPen))]
    private Guid? _editingCustomPenId;

    public bool IsEditingExistingCustomPen => EditingCustomPenId.HasValue;

    [ObservableProperty]
    private bool _isCustomPenEditorOpen;

    partial void OnIsCustomPenEditorOpenChanged(bool value)
    {
        if (value)
        {
            IsPenOptionsOpen = false;
            IsEraserOptionsOpen = false;
            IsLassoOptionsOpen = false;
            IsTextOptionsOpen = false;
            IsCustomColorPickerOpen = false;
        }
        else
        {
            CustomPenEditorShortcutError = null;
        }
    }

    [ObservableProperty]
    private string _customPenEditorName = "Custom Pen";

    [ObservableProperty]
    private string _customPenEditorColorHex = DefaultPenColorHex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomPenEditorPreviewDiameter))]
    private double _customPenEditorThickness = DefaultPenThickness;

    [ObservableProperty]
    private string _customPenEditorShortcut = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomPenEditorShortcutError))]
    private string? _customPenEditorShortcutError;

    public bool HasCustomPenEditorShortcutError => !string.IsNullOrWhiteSpace(CustomPenEditorShortcutError);

    [ObservableProperty]
    private SolidColorBrush _customPenEditorPreviewBrush = null!;

    public double CustomPenEditorPreviewDiameter => Math.Clamp(CustomPenEditorThickness * 2.0 + 3.0, 6.0, 24.0);

    partial void OnIsPenOptionsOpenChanged(bool value)
    {
        if (value)
        {
            IsEraserOptionsOpen = false;
            IsLassoOptionsOpen = false;
            IsTextOptionsOpen = false;
            IsCustomPenEditorOpen = false;
        }
        else
        {
            IsCustomColorPickerOpen = false;
        }
    }

    partial void OnIsEraserOptionsOpenChanged(bool value)
    {
        if (value)
        {
            IsPenOptionsOpen = false;
            IsLassoOptionsOpen = false;
            IsTextOptionsOpen = false;
            IsCustomColorPickerOpen = false;
            IsCustomPenEditorOpen = false;
        }
    }

    partial void OnIsLassoOptionsOpenChanged(bool value)
    {
        if (value)
        {
            IsPenOptionsOpen = false;
            IsEraserOptionsOpen = false;
            IsTextOptionsOpen = false;
            IsCustomColorPickerOpen = false;
            IsCustomPenEditorOpen = false;
        }
    }

    partial void OnIsTextOptionsOpenChanged(bool value)
    {
        if (value)
        {
            IsPenOptionsOpen = false;
            IsEraserOptionsOpen = false;
            IsLassoOptionsOpen = false;
            IsCustomPenEditorOpen = false;
        }
        else
        {
            IsCustomColorPickerOpen = false;
        }
    }

    private readonly UserSettingsService? _settingsService;
    private readonly UserSettings _userSettings;
    private bool _isSyncingCustomColor;

    public static IReadOnlyList<ColorSwatchOption> PaletteGridSwatches { get; } = CreatePaletteGridSwatches();

    public ToolbarViewModel()
        : this(null)
    {
    }

    public ToolbarViewModel(UserSettingsService? settingsService)
    {
        _settingsService = settingsService;
        _userSettings = settingsService?.Load() ?? new UserSettings();

        var initialPenSwatches = CreateFrozenSwatches();
        var initialTextSwatches = CreateFrozenSwatches();

        ColorSwatches = new ObservableCollection<ColorSwatchOption>(initialPenSwatches);
        TextColorSwatches = new ObservableCollection<ColorSwatchOption>(initialTextSwatches);

        foreach (var hex in _userSettings.CustomColorHexes)
        {
            if (TryParseRgb(hex, out var r, out var g, out var b))
            {
                var brush1 = new SolidColorBrush(Color.FromRgb(r, g, b));
                brush1.Freeze();
                ColorSwatches.Add(new ColorSwatchOption(hex, brush1, isCustom: true));

                var brush2 = new SolidColorBrush(Color.FromRgb(r, g, b));
                brush2.Freeze();
                TextColorSwatches.Add(new ColorSwatchOption(hex, brush2, isCustom: true));
            }
        }

        CustomPenPresets = new ObservableCollection<CustomPenPresetViewModel>();
        CustomPenPresets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCustomPenPresets));

        if (_userSettings.CustomPenPresets is not null)
        {
            foreach (var preset in _userSettings.CustomPenPresets)
            {
                if (TryParseRgb(preset.ColorHex, out var pr, out var pg, out var pb))
                {
                    var brush = new SolidColorBrush(Color.FromRgb(pr, pg, pb));
                    brush.Freeze();
                    CustomPenPresets.Add(new CustomPenPresetViewModel(
                        preset.Id,
                        preset.Name,
                        preset.ColorHex,
                        preset.ThicknessDip,
                        preset.Shortcut,
                        brush));
                }
            }
        }

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

        SetCustomColorRgb(0x25, 0x63, 0xEB);
        UpdateCustomPenEditorPreviewBrush();
    }

    public DrawingAttributes CurrentDrawingAttributes { get; }
    public ObservableCollection<ColorSwatchOption> ColorSwatches { get; }
    public ObservableCollection<ColorSwatchOption> TextColorSwatches { get; }
    public ObservableCollection<CustomPenPresetViewModel> CustomPenPresets { get; }
    public bool HasCustomPenPresets => CustomPenPresets.Count > 0;
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

    partial void OnCustomColorRChanged(byte value)
    {
        if (_isSyncingCustomColor) return;
        _isSyncingCustomColor = true;
        try { SyncFromRgb(); }
        finally { _isSyncingCustomColor = false; }
    }

    partial void OnCustomColorGChanged(byte value)
    {
        if (_isSyncingCustomColor) return;
        _isSyncingCustomColor = true;
        try { SyncFromRgb(); }
        finally { _isSyncingCustomColor = false; }
    }

    partial void OnCustomColorBChanged(byte value)
    {
        if (_isSyncingCustomColor) return;
        _isSyncingCustomColor = true;
        try { SyncFromRgb(); }
        finally { _isSyncingCustomColor = false; }
    }

    partial void OnCustomColorHexInputChanged(string value)
    {
        if (_isSyncingCustomColor) return;
        if (UserSettingsService.TryNormalizeColorHex(value, out _, out var r, out var g, out var b))
        {
            _isSyncingCustomColor = true;
            try
            {
                CustomColorR = r;
                CustomColorG = g;
                CustomColorB = b;
                SyncFromRgb(skipHexUpdate: true);
            }
            finally { _isSyncingCustomColor = false; }
        }
    }

    partial void OnCustomColorHueChanged(double value)
    {
        if (_isSyncingCustomColor) return;
        _isSyncingCustomColor = true;
        try { SyncFromHsv(); }
        finally { _isSyncingCustomColor = false; }
    }

    [RelayCommand]
    public void ToggleCustomColorPicker()
    {
        IsCustomColorPickerOpen = !IsCustomColorPickerOpen;
        if (IsCustomColorPickerOpen)
        {
            var activeHex = IsTextOptionsOpen || ActiveTool == ToolMode.Text
                ? SelectedTextColorHex
                : (ActiveTool == ToolMode.Highlighter ? SelectedHighlighterColorHex : SelectedPenColorHex);

            if (TryParseRgb(activeHex, out var r, out var g, out var b))
            {
                SetCustomColorRgb(r, g, b);
            }
        }
    }

    [RelayCommand]
    public void PickPaletteColor(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex) && TryParseRgb(hex, out var r, out var g, out var b))
        {
            SetCustomColorRgb(r, g, b);
        }
    }

    [RelayCommand]
    public void AddCustomColor()
    {
        var hex = string.Create(CultureInfo.InvariantCulture, $"#FF{CustomColorR:X2}{CustomColorG:X2}{CustomColorB:X2}");

        if (!ColorSwatches.Any(s => string.Equals(s.Hex, hex, StringComparison.OrdinalIgnoreCase)))
        {
            var brush1 = new SolidColorBrush(Color.FromRgb(CustomColorR, CustomColorG, CustomColorB));
            brush1.Freeze();
            ColorSwatches.Add(new ColorSwatchOption(hex, brush1, isCustom: true));

            var brush2 = new SolidColorBrush(Color.FromRgb(CustomColorR, CustomColorG, CustomColorB));
            brush2.Freeze();
            TextColorSwatches.Add(new ColorSwatchOption(hex, brush2, isCustom: true));

            if (!_userSettings.CustomColorHexes.Contains(hex, StringComparer.OrdinalIgnoreCase))
            {
                _userSettings.CustomColorHexes.Add(hex);
                SaveUserSettings();
            }
        }

        if (IsTextOptionsOpen || ActiveTool == ToolMode.Text)
        {
            SelectTextColor(hex);
        }
        else
        {
            SelectColorSwatch(hex);
        }

        IsCustomColorPickerOpen = false;
    }

    [RelayCommand]
    public void RemoveCustomColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        var matchPen = ColorSwatches.FirstOrDefault(s => s.IsCustom && string.Equals(s.Hex, hex, StringComparison.OrdinalIgnoreCase));
        if (matchPen is not null)
        {
            ColorSwatches.Remove(matchPen);
        }

        var matchText = TextColorSwatches.FirstOrDefault(s => s.IsCustom && string.Equals(s.Hex, hex, StringComparison.OrdinalIgnoreCase));
        if (matchText is not null)
        {
            TextColorSwatches.Remove(matchText);
        }

        var removed = _userSettings.CustomColorHexes.RemoveAll(h => string.Equals(h, hex, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SaveUserSettings();
        }

        if (string.Equals(SelectedPenColorHex, hex, StringComparison.OrdinalIgnoreCase))
        {
            SelectedPenColorHex = DefaultPenColorHex;
            ApplyAttributesToSharedInstance();
        }

        if (string.Equals(SelectedTextColorHex, hex, StringComparison.OrdinalIgnoreCase))
        {
            SelectedTextColorHex = DefaultTextColorHex;
        }

        UpdateSubtoolSelectionStates();
    }

    [RelayCommand]
    public void ActivateOrCycleEraser()
    {
        IsPenOptionsOpen = false;
        IsEraserOptionsOpen = false;
        IsLassoOptionsOpen = false;
        IsTextOptionsOpen = false;
        IsCustomPenEditorOpen = false;

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
        IsCustomPenEditorOpen = false;

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
                IsCustomPenEditorOpen = false;
                IsPenOptionsOpen = !IsPenOptionsOpen;
                return;
            }

            if (mode == ToolMode.Eraser)
            {
                IsPenOptionsOpen = false;
                IsLassoOptionsOpen = false;
                IsTextOptionsOpen = false;
                IsCustomPenEditorOpen = false;
                IsEraserOptionsOpen = !IsEraserOptionsOpen;
                return;
            }

            if (mode == ToolMode.Lasso)
            {
                IsPenOptionsOpen = false;
                IsEraserOptionsOpen = false;
                IsTextOptionsOpen = false;
                IsCustomPenEditorOpen = false;
                IsLassoOptionsOpen = !IsLassoOptionsOpen;
                return;
            }

            if (mode == ToolMode.Text)
            {
                IsPenOptionsOpen = false;
                IsEraserOptionsOpen = false;
                IsLassoOptionsOpen = false;
                IsCustomPenEditorOpen = false;
                IsTextOptionsOpen = !IsTextOptionsOpen;
                return;
            }
        }

        IsPenOptionsOpen = false;
        IsEraserOptionsOpen = false;
        IsLassoOptionsOpen = false;
        IsTextOptionsOpen = false;
        IsCustomPenEditorOpen = false;
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

        if (CustomPenPresets is not null)
        {
            foreach (var preset in CustomPenPresets)
            {
                preset.IsSelected = ActiveTool == ToolMode.Pen &&
                                    string.Equals(preset.ColorHex, SelectedPenColorHex, StringComparison.OrdinalIgnoreCase) &&
                                    Math.Abs(preset.ThicknessDip - SelectedPenThickness) < 0.05;
            }
        }
    }

    [RelayCommand]
    public void OpenNewCustomPenEditor()
    {
        EditingCustomPenId = null;
        CustomPenEditorName = SuggestNewCustomPenName();
        CustomPenEditorColorHex = SelectedPenColorHex;
        CustomPenEditorThickness = SelectedPenThickness;
        CustomPenEditorShortcut = SuggestNextNumericShortcut();
        CustomPenEditorShortcutError = null;
        UpdateCustomPenEditorPreviewBrush();
        IsCustomPenEditorOpen = true;
    }

    [RelayCommand]
    public void OpenEditCustomPenEditor(CustomPenPresetViewModel? preset)
    {
        if (preset is null)
        {
            return;
        }

        EditingCustomPenId = preset.Id;
        CustomPenEditorName = preset.Name;
        CustomPenEditorColorHex = preset.ColorHex;
        CustomPenEditorThickness = preset.ThicknessDip;
        CustomPenEditorShortcut = preset.Shortcut;
        CustomPenEditorShortcutError = null;
        UpdateCustomPenEditorPreviewBrush();
        IsCustomPenEditorOpen = true;
    }

    [RelayCommand]
    public void CloseCustomPenEditor()
    {
        IsCustomPenEditorOpen = false;
        CustomPenEditorShortcutError = null;
    }

    [RelayCommand]
    public void ClearCustomPenEditorShortcut()
    {
        CustomPenEditorShortcut = string.Empty;
        CustomPenEditorShortcutError = null;
    }

    [RelayCommand]
    public void SelectCustomPenEditorColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        if (UserSettingsService.TryNormalizeColorHex(hex, out var normalized, out _, out _, out _))
        {
            CustomPenEditorColorHex = normalized;
            UpdateCustomPenEditorPreviewBrush();
        }
    }

    [RelayCommand]
    public void SelectCustomPenEditorThickness(double thickness)
    {
        if (!double.IsFinite(thickness) || thickness <= 0.0)
        {
            return;
        }

        CustomPenEditorThickness = Math.Clamp(thickness, 0.5, 32.0);
        OnPropertyChanged(nameof(CustomPenEditorPreviewDiameter));
    }

    [RelayCommand]
    public void SaveCustomPenPreset()
    {
        var trimmedName = CustomPenEditorName?.Trim();
        var finalName = string.IsNullOrWhiteSpace(trimmedName) ? "Custom Pen" : trimmedName;
        if (finalName.Length > 64)
        {
            finalName = finalName[..64];
        }

        if (!UserSettingsService.TryNormalizeColorHex(CustomPenEditorColorHex, out var normalizedColor, out var r, out var g, out var b))
        {
            normalizedColor = DefaultPenColorHex;
            r = 0x1E; g = 0x1E; b = 0x1E;
        }

        var clampedThickness = Math.Clamp(CustomPenEditorThickness, 0.5, 32.0);

        if (!TryValidateCustomPenShortcut(CustomPenEditorShortcut, EditingCustomPenId, out var normalizedShortcut, out var shortcutError))
        {
            CustomPenEditorShortcutError = shortcutError;
            return;
        }

        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();

        CustomPenPresetViewModel? targetVm = null;
        if (EditingCustomPenId.HasValue)
        {
            targetVm = CustomPenPresets.FirstOrDefault(p => p.Id == EditingCustomPenId.Value);
        }

        if (targetVm is not null)
        {
            targetVm.UpdateFrom(finalName, normalizedColor, clampedThickness, normalizedShortcut, brush);

            var settingPreset = _userSettings.CustomPenPresets.FirstOrDefault(p => p.Id == targetVm.Id);
            if (settingPreset is not null)
            {
                settingPreset.Name = finalName;
                settingPreset.ColorHex = normalizedColor;
                settingPreset.ThicknessDip = clampedThickness;
                settingPreset.Shortcut = normalizedShortcut;
            }
            else
            {
                _userSettings.CustomPenPresets.Add(new CustomPenPreset
                {
                    Id = targetVm.Id,
                    Name = finalName,
                    ColorHex = normalizedColor,
                    ThicknessDip = clampedThickness,
                    Shortcut = normalizedShortcut
                });
            }
        }
        else
        {
            var newId = Guid.CreateVersion7();
            targetVm = new CustomPenPresetViewModel(newId, finalName, normalizedColor, clampedThickness, normalizedShortcut, brush);
            CustomPenPresets.Add(targetVm);

            _userSettings.CustomPenPresets.Add(new CustomPenPreset
            {
                Id = newId,
                Name = finalName,
                ColorHex = normalizedColor,
                ThicknessDip = clampedThickness,
                Shortcut = normalizedShortcut
            });
        }

        SaveUserSettings();
        ActivateCustomPen(targetVm);
        IsCustomPenEditorOpen = false;
    }

    [RelayCommand]
    public void DeleteCustomPenPreset(object? parameter)
    {
        Guid? idToDelete = parameter as Guid? ??
            (parameter as CustomPenPresetViewModel)?.Id ??
            EditingCustomPenId;

        if (!idToDelete.HasValue)
        {
            return;
        }

        var match = CustomPenPresets.FirstOrDefault(p => p.Id == idToDelete.Value);
        if (match is not null)
        {
            CustomPenPresets.Remove(match);
        }

        var removed = _userSettings.CustomPenPresets.RemoveAll(p => p.Id == idToDelete.Value);
        if (removed > 0)
        {
            SaveUserSettings();
        }

        IsCustomPenEditorOpen = false;
        UpdateSubtoolSelectionStates();
    }

    [RelayCommand]
    public void ActivateCustomPen(CustomPenPresetViewModel? preset)
    {
        if (preset is null)
        {
            return;
        }

        ActiveTool = ToolMode.Pen;
        SelectedPenColorHex = preset.ColorHex;
        SelectedPenThickness = preset.ThicknessDip;

        IsPenOptionsOpen = false;
        IsEraserOptionsOpen = false;
        IsLassoOptionsOpen = false;
        IsTextOptionsOpen = false;
        IsCustomPenEditorOpen = false;

        ApplyAttributesToSharedInstance();
        UpdateSubtoolSelectionStates();
    }

    public bool TryActivateCustomPenByShortcut(Key key, ModifierKeys modifiers)
    {
        if (!TryFormatKey(key, modifiers, out var formatted))
        {
            return false;
        }

        var match = CustomPenPresets.FirstOrDefault(p =>
            string.Equals(p.Shortcut, formatted, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            ActivateCustomPen(match);
            return true;
        }

        return false;
    }

    public bool TrySetCustomPenEditorShortcutFromKey(Key key, ModifierKeys modifiers)
    {
        if (TryFormatKey(key, modifiers, out var formatted))
        {
            if (TryValidateCustomPenShortcut(formatted, EditingCustomPenId, out var normalized, out var error))
            {
                CustomPenEditorShortcut = normalized;
                CustomPenEditorShortcutError = null;
                return true;
            }
            else
            {
                CustomPenEditorShortcut = formatted;
                CustomPenEditorShortcutError = error;
                return false;
            }
        }

        return false;
    }

    public bool TryValidateCustomPenShortcut(string? raw, Guid? editingId, out string normalized, out string? error)
    {
        error = null;
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();
        var parts = trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hasCtrl = parts.Any(p => string.Equals(p, "ctrl", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "control", StringComparison.OrdinalIgnoreCase));
        var hasAlt = parts.Any(p => string.Equals(p, "alt", StringComparison.OrdinalIgnoreCase));
        var hasShift = parts.Any(p => string.Equals(p, "shift", StringComparison.OrdinalIgnoreCase));
        var keyName = parts.LastOrDefault(p => !string.Equals(p, "ctrl", StringComparison.OrdinalIgnoreCase) &&
                                               !string.Equals(p, "control", StringComparison.OrdinalIgnoreCase) &&
                                               !string.Equals(p, "alt", StringComparison.OrdinalIgnoreCase) &&
                                               !string.Equals(p, "shift", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(keyName))
        {
            error = "Invalid shortcut format.";
            return false;
        }

        keyName = keyName.ToUpperInvariant();
        var sb = new System.Text.StringBuilder();
        if (hasCtrl) sb.Append("Ctrl+");
        if (hasAlt) sb.Append("Alt+");
        if (hasShift) sb.Append("Shift+");
        sb.Append(keyName);
        normalized = sb.ToString();

        if (!hasCtrl && !hasAlt && !hasShift)
        {
            string[] reservedSingleKeys = ["V", "P", "H", "E", "L", "T", "I", "F1", "F2", "F5", "F11", "DELETE", "BACK", "BACKSPACE", "SPACE", "ESCAPE", "ENTER", "RETURN", "PAGEUP", "PAGEDOWN", "LEFT", "RIGHT", "UP", "DOWN", "TAB"];
            if (reservedSingleKeys.Contains(keyName, StringComparer.OrdinalIgnoreCase))
            {
                error = $"'{normalized}' is a reserved shortcut.";
                return false;
            }
        }

        if (hasCtrl && !hasAlt && !hasShift)
        {
            string[] reservedCtrlKeys = ["N", "O", "S", "E", "C", "V", "Z", "Y", "0", "ENTER", "RETURN"];
            if (reservedCtrlKeys.Contains(keyName, StringComparer.OrdinalIgnoreCase))
            {
                error = $"'{normalized}' is a reserved shortcut.";
                return false;
            }
        }

        var candidate = normalized;
        var conflict = CustomPenPresets.FirstOrDefault(p =>
            (editingId == null || p.Id != editingId.Value) &&
            string.Equals(p.Shortcut, candidate, StringComparison.OrdinalIgnoreCase));

        if (conflict is not null)
        {
            error = $"Shortcut '{normalized}' is already used by '{conflict.Name}'.";
            return false;
        }

        return true;
    }

    public static bool TryFormatKey(Key key, ModifierKeys modifiers, out string formatted)
    {
        formatted = string.Empty;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            return false;
        }

        string keyPart;
        if (key >= Key.D0 && key <= Key.D9)
        {
            keyPart = ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture);
        }
        else if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            keyPart = ((int)(key - Key.NumPad0)).ToString(CultureInfo.InvariantCulture);
        }
        else if (key >= Key.A && key <= Key.Z)
        {
            keyPart = key.ToString().ToUpperInvariant();
        }
        else if (key >= Key.F1 && key <= Key.F24)
        {
            keyPart = key.ToString().ToUpperInvariant();
        }
        else
        {
            return false;
        }

        var sb = new System.Text.StringBuilder();
        if ((modifiers & ModifierKeys.Control) != 0) sb.Append("Ctrl+");
        if ((modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt+");
        if ((modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift+");
        sb.Append(keyPart);

        formatted = sb.ToString();
        return true;
    }

    private void UpdateCustomPenEditorPreviewBrush()
    {
        if (TryParseRgb(CustomPenEditorColorHex, out var r, out var g, out var b))
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            CustomPenEditorPreviewBrush = brush;
        }
        else
        {
            var brush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            brush.Freeze();
            CustomPenEditorPreviewBrush = brush;
        }
    }

    private string SuggestNextNumericShortcut()
    {
        for (var i = 1; i <= 9; i++)
        {
            var digitStr = i.ToString(CultureInfo.InvariantCulture);
            if (!CustomPenPresets.Any(p => string.Equals(p.Shortcut, digitStr, StringComparison.OrdinalIgnoreCase)))
            {
                return digitStr;
            }
        }

        return string.Empty;
    }

    private string SuggestNewCustomPenName()
    {
        for (var i = 1; i <= 50; i++)
        {
            var name = $"Custom Pen {i}";
            if (!CustomPenPresets.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
        }

        return "Custom Pen";
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

    public void SetCustomColorRgb(byte r, byte g, byte b)
    {
        _isSyncingCustomColor = true;
        try
        {
            CustomColorR = r;
            CustomColorG = g;
            CustomColorB = b;
            SyncFromRgb();
        }
        finally
        {
            _isSyncingCustomColor = false;
        }
    }

    public void SetCustomColorSaturationAndValue(double s, double v)
    {
        _isSyncingCustomColor = true;
        try
        {
            CustomColorSaturation = Math.Clamp(s, 0.0, 1.0);
            CustomColorValue = Math.Clamp(v, 0.0, 1.0);
            SyncFromHsv();
        }
        finally
        {
            _isSyncingCustomColor = false;
        }
    }

    public void SetCustomColorHue(double h)
    {
        _isSyncingCustomColor = true;
        try
        {
            CustomColorHue = Math.Clamp(h, 0.0, 360.0);
            SyncFromHsv();
        }
        finally
        {
            _isSyncingCustomColor = false;
        }
    }

    private void SyncFromRgb(bool skipHexUpdate = false)
    {
        RgbToHsv(CustomColorR, CustomColorG, CustomColorB, out var h, out var s, out var v);
        CustomColorHue = h;
        CustomColorSaturation = s;
        CustomColorValue = v;

        if (!skipHexUpdate)
        {
            CustomColorHexInput = $"#{CustomColorR:X2}{CustomColorG:X2}{CustomColorB:X2}";
        }

        UpdatePreviewBrushes();
    }

    private void SyncFromHsv()
    {
        HsvToRgb(CustomColorHue, CustomColorSaturation, CustomColorValue, out var r, out var g, out var b);
        CustomColorR = r;
        CustomColorG = g;
        CustomColorB = b;
        CustomColorHexInput = $"#{r:X2}{g:X2}{b:X2}";

        UpdatePreviewBrushes();
    }

    private void UpdatePreviewBrushes()
    {
        var preview = new SolidColorBrush(Color.FromRgb(CustomColorR, CustomColorG, CustomColorB));
        preview.Freeze();
        CustomColorPreviewBrush = preview;

        HsvToRgb(CustomColorHue, 1.0, 1.0, out var hr, out var hg, out var hb);
        var hueBase = new SolidColorBrush(Color.FromRgb(hr, hg, hb));
        hueBase.Freeze();
        CustomHueBaseBrush = hueBase;
    }

    public static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        v = max;
        s = max == 0.0 ? 0.0 : delta / max;

        if (delta == 0.0)
        {
            h = 0.0;
        }
        else if (Math.Abs(max - rd) < 0.0001)
        {
            h = 60.0 * (((gd - bd) / delta) % 6.0);
            if (h < 0.0) h += 360.0;
        }
        else if (Math.Abs(max - gd) < 0.0001)
        {
            h = 60.0 * (((bd - rd) / delta) + 2.0);
        }
        else
        {
            h = 60.0 * (((rd - gd) / delta) + 4.0);
        }
    }

    public static void HsvToRgb(double h, double s, double v, out byte r, out byte g, out byte b)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);

        double c = v * s;
        double x = c * (1.0 - Math.Abs((h / 60.0 % 2.0) - 1.0));
        double m = v - c;

        double rd = 0, gd = 0, bd = 0;
        if (h < 60) { rd = c; gd = x; bd = 0; }
        else if (h < 120) { rd = x; gd = c; bd = 0; }
        else if (h < 180) { rd = 0; gd = c; bd = x; }
        else if (h < 240) { rd = 0; gd = x; bd = c; }
        else if (h < 300) { rd = x; gd = 0; bd = c; }
        else { rd = c; gd = 0; bd = x; }

        r = (byte)Math.Clamp(Math.Round((rd + m) * 255.0), 0, 255);
        g = (byte)Math.Clamp(Math.Round((gd + m) * 255.0), 0, 255);
        b = (byte)Math.Clamp(Math.Round((bd + m) * 255.0), 0, 255);
    }

    private static List<ColorSwatchOption> CreatePaletteGridSwatches()
    {
        var hexes = new[]
        {
            "#FFFCA5A5", "#FFFDBA74", "#FFFDE047", "#FF86EFAC", "#FF67E8F9", "#FF93C5FD", "#FFC4B5FD", "#FFF472B6",
            "#FFEF4444", "#FFF97316", "#FFEAB308", "#FF22C55E", "#FF06B6D4", "#FF3B82F6", "#FF8B5CF6", "#FFEC4899",
            "#FF991B1B", "#FFC2410C", "#FFA16207", "#FF15803D", "#FF0E7490", "#FF1D4ED8", "#FF6D28D9", "#FFBE185D"
        };
        var list = new List<ColorSwatchOption>(hexes.Length);
        foreach (var hex in hexes)
        {
            if (TryParseRgb(hex, out var r, out var g, out var b))
            {
                var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                brush.Freeze();
                list.Add(new ColorSwatchOption(hex, brush));
            }
        }
        return list;
    }

    private void SaveUserSettings()
    {
        _settingsService?.Save(_userSettings);
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
