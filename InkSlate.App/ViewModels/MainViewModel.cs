using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public const double MinZoomLevel = 0.25;
    public const double MaxZoomLevel = 5.0;
    public const double DefaultZoomLevel = 1.0;

    [ObservableProperty]
    private ObservableCollection<PageViewModel> _pages = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePageIndex))]
    [NotifyPropertyChangedFor(nameof(PageIndicatorDisplay))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeletePageCommand))]
    private PageViewModel? _activePage;

    [ObservableProperty]
    private ViewMode _currentViewMode = ViewMode.Consecutive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomPercentageDisplay))]
    private double _zoomLevel = DefaultZoomLevel;

    [ObservableProperty]
    private bool _isPageSetupOpen;

    [ObservableProperty]
    private PageDimensionPreset _selectedPreset = PageDimensionPreset.A4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DimensionUnitLabel))]
    private bool _isMillimeterUnit;

    [ObservableProperty]
    private string _customWidthInput = "793.7";

    [ObservableProperty]
    private string _customHeightInput = "1122.5";

    [ObservableProperty]
    private bool _hasDimensionInputError;

    public MainViewModel()
        : this(new NoteDocument(), new UndoRedoManager())
    {
    }

    public MainViewModel(NoteDocument document, UndoRedoManager undoRedoManager)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(undoRedoManager);

        Document = document;
        UndoRedo = undoRedoManager;
        UndoRedo.StateChanged += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };

        if (document.Pages.Count == 0)
        {
            var initialPage = new NotePage
            {
                PageNumber = 1,
                Width = document.DefaultPageWidth,
                Height = document.DefaultPageHeight
            };
            document.Pages.Add(initialPage);
        }

        foreach (var page in document.Pages)
        {
            Pages.Add(new PageViewModel(page));
        }

        ActivePage = Pages[0];
        SyncDimensionInputsFromDimensions(new PageDimensions(ActivePage.Width, ActivePage.Height));
    }

    public NoteDocument Document { get; }
    public UndoRedoManager UndoRedo { get; }

    public int ActivePageIndex => ActivePage is null ? 1 : Math.Max(1, Pages.IndexOf(ActivePage) + 1);
    public int TotalPages => Pages.Count;
    public string PageIndicatorDisplay => $"{ActivePageIndex} / {Math.Max(1, Pages.Count)}";
    public string ZoomPercentageDisplay => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(ZoomLevel * 100.0):0}%");
    public string DimensionUnitLabel => IsMillimeterUnit ? "mm" : "px";

    public void SetZoom(double requestedZoom)
    {
        var safeZoom = double.IsFinite(requestedZoom)
            ? Math.Clamp(requestedZoom, MinZoomLevel, MaxZoomLevel)
            : DefaultZoomLevel;
        ZoomLevel = safeZoom;
    }

    partial void OnActivePageChanged(PageViewModel? oldValue, PageViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
            SyncDimensionInputsFromDimensions(new PageDimensions(newValue.Width, newValue.Height));
        }
    }

    partial void OnZoomLevelChanged(double value)
    {
        if (!double.IsFinite(value))
        {
            ZoomLevel = DefaultZoomLevel;
        }
        else if (value < MinZoomLevel || value > MaxZoomLevel)
        {
            ZoomLevel = Math.Clamp(value, MinZoomLevel, MaxZoomLevel);
        }
    }

    private bool CanUndo() => UndoRedo.CanUndo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRedo.Undo();

    private bool CanRedo() => UndoRedo.CanRedo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => UndoRedo.Redo();

    [RelayCommand]
    private void AddPage()
    {
        var nextNumber = Pages.Count + 1;
        var model = new NotePage
        {
            PageNumber = nextNumber,
            Width = Document.DefaultPageWidth,
            Height = Document.DefaultPageHeight
        };

        Document.Pages.Add(model);
        var pageVm = new PageViewModel(model);
        Pages.Add(pageVm);
        ActivePage = pageVm;
        NotifyPageCountChanged();
    }

    private bool CanDeletePage() => Pages.Count > 1 && ActivePage is not null;

    [RelayCommand(CanExecute = nameof(CanDeletePage))]
    private void DeletePage()
    {
        if (Pages.Count <= 1 || ActivePage is null)
        {
            return;
        }

        var index = Pages.IndexOf(ActivePage);
        if (index < 0)
        {
            return;
        }

        var removed = ActivePage;
        Pages.RemoveAt(index);
        Document.Pages.Remove(removed.Model);

        for (var i = 0; i < Pages.Count; i++)
        {
            Pages[i].PageNumber = i + 1;
        }

        var nextActiveIndex = Math.Clamp(index, 0, Pages.Count - 1);
        ActivePage = Pages[nextActiveIndex];
        NotifyPageCountChanged();
    }

    private bool CanGoNextPage() => ActivePage is not null && Pages.IndexOf(ActivePage) < Pages.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void NextPage()
    {
        if (ActivePage is null)
        {
            return;
        }

        var index = Pages.IndexOf(ActivePage);
        if (index >= 0 && index + 1 < Pages.Count)
        {
            ActivePage = Pages[index + 1];
        }
    }

    private bool CanGoPreviousPage() => ActivePage is not null && Pages.IndexOf(ActivePage) > 0;

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void PreviousPage()
    {
        if (ActivePage is null)
        {
            return;
        }

        var index = Pages.IndexOf(ActivePage);
        if (index > 0)
        {
            ActivePage = Pages[index - 1];
        }
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        CurrentViewMode = CurrentViewMode == ViewMode.Consecutive
            ? ViewMode.Separated
            : ViewMode.Consecutive;
    }

    [RelayCommand]
    private void ResetZoom() => ZoomLevel = DefaultZoomLevel;

    [RelayCommand]
    private void TogglePageSetup()
    {
        if (!IsPageSetupOpen && ActivePage is not null)
        {
            SyncDimensionInputsFromDimensions(new PageDimensions(ActivePage.Width, ActivePage.Height));
            HasDimensionInputError = false;
        }

        IsPageSetupOpen = !IsPageSetupOpen;
    }

    [RelayCommand]
    private void SelectPreset(PageDimensionPreset preset)
    {
        SelectedPreset = preset;
        HasDimensionInputError = false;

        if (preset != PageDimensionPreset.Custom)
        {
            var dims = PageDimensions.FromPreset(preset);
            FormatDimensionInputs(dims);
        }
    }

    [RelayCommand]
    private void ToggleDimensionUnit()
    {
        if (TryParseCurrentInputsAsDip(out var currentDims))
        {
            IsMillimeterUnit = !IsMillimeterUnit;
            FormatDimensionInputs(currentDims);
        }
        else
        {
            IsMillimeterUnit = !IsMillimeterUnit;
        }
    }

    [RelayCommand]
    private void ApplyPageDimensions()
    {
        if (!TryParseCurrentInputsAsDip(out var validDims))
        {
            HasDimensionInputError = true;
            return;
        }

        HasDimensionInputError = false;
        Document.DefaultPageWidth = validDims.Width;
        Document.DefaultPageHeight = validDims.Height;

        if (ActivePage is not null)
        {
            ActivePage.Width = validDims.Width;
            ActivePage.Height = validDims.Height;
        }

        IsPageSetupOpen = false;
    }

    private bool TryParseCurrentInputsAsDip(out PageDimensions dimensions)
    {
        dimensions = default;

        if (!double.TryParse(CustomWidthInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var rawWidth) ||
            !double.TryParse(CustomHeightInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var rawHeight) ||
            !double.IsFinite(rawWidth) ||
            !double.IsFinite(rawHeight))
        {
            return false;
        }

        var widthDip = IsMillimeterUnit ? PageDimensions.MillimetersToDip(rawWidth) : rawWidth;
        var heightDip = IsMillimeterUnit ? PageDimensions.MillimetersToDip(rawHeight) : rawHeight;

        return PageDimensions.TryCreateCustom(widthDip, heightDip, out dimensions);
    }

    private void SyncDimensionInputsFromDimensions(PageDimensions dims)
    {
        SelectedPreset = DetectPreset(dims);
        FormatDimensionInputs(dims);
    }

    private void FormatDimensionInputs(PageDimensions dims)
    {
        if (IsMillimeterUnit)
        {
            var wMm = Math.Round(PageDimensions.DipToMillimeters(dims.Width), 1);
            var hMm = Math.Round(PageDimensions.DipToMillimeters(dims.Height), 1);
            CustomWidthInput = wMm.ToString("0.#", CultureInfo.InvariantCulture);
            CustomHeightInput = hMm.ToString("0.#", CultureInfo.InvariantCulture);
        }
        else
        {
            CustomWidthInput = Math.Round(dims.Width, 1).ToString("0.#", CultureInfo.InvariantCulture);
            CustomHeightInput = Math.Round(dims.Height, 1).ToString("0.#", CultureInfo.InvariantCulture);
        }
    }

    private static PageDimensionPreset DetectPreset(PageDimensions dims)
    {
        const double tolerance = 0.5;
        if (AreClose(dims, PageDimensions.A4, tolerance)) return PageDimensionPreset.A4;
        if (AreClose(dims, PageDimensions.Letter, tolerance)) return PageDimensionPreset.Letter;
        if (AreClose(dims, PageDimensions.Legal, tolerance)) return PageDimensionPreset.Legal;
        if (AreClose(dims, PageDimensions.Slide16x9, tolerance)) return PageDimensionPreset.Slide16x9;
        return PageDimensionPreset.Custom;
    }

    private static bool AreClose(PageDimensions a, PageDimensions b, double tolerance) =>
        Math.Abs(a.Width - b.Width) <= tolerance && Math.Abs(a.Height - b.Height) <= tolerance;

    private void NotifyPageCountChanged()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(ActivePageIndex));
        OnPropertyChanged(nameof(PageIndicatorDisplay));
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        DeletePageCommand.NotifyCanExecuteChanged();
    }
}
