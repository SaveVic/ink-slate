using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Models;
using InkSlate.Core.Services;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public const double MinZoomLevel = 0.25;
    public const double MaxZoomLevel = 5.0;
    public const double DefaultZoomLevel = 1.0;
    public const double ConsecutivePageGap = 24.0;

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
    [NotifyPropertyChangedFor(nameof(IsConsecutiveView))]
    [NotifyPropertyChangedFor(nameof(IsSeparatedView))]
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

    public event Action<PageViewModel>? PageNavigationScrollRequested;

    public MainViewModel()
        : this(new NoteDocument(), new UndoRedoManager(), new ToolbarViewModel())
    {
    }

    public MainViewModel(NoteDocument document, UndoRedoManager undoRedoManager, ToolbarViewModel? toolbar = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(undoRedoManager);

        Document = document;
        UndoRedo = undoRedoManager;
        Toolbar = toolbar ?? new ToolbarViewModel();
        Toolbar.PropertyChanged += (_, e) =>
        {
            if ((e.PropertyName == nameof(Toolbar.IsPenOptionsOpen) && Toolbar.IsPenOptionsOpen) ||
                (e.PropertyName == nameof(Toolbar.IsEraserOptionsOpen) && Toolbar.IsEraserOptionsOpen))
            {
                IsPageSetupOpen = false;
            }
        };

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
            Pages.Add(new PageViewModel(page, UndoRedo, Toolbar, Document.Assets.GetValueOrDefault, (k, b) => Document.Assets[k] = b));
        }

        ActivePage = Pages[0];
        ActivePage.Hydrate();
        SyncDimensionInputsFromDimensions(new PageDimensions(ActivePage.Width, ActivePage.Height));
    }

    public NoteDocument Document { get; }
    public UndoRedoManager UndoRedo { get; }
    public ToolbarViewModel Toolbar { get; }

    public bool IsConsecutiveView => CurrentViewMode == ViewMode.Consecutive;
    public bool IsSeparatedView => CurrentViewMode == ViewMode.Separated;

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

    public double GetPageVerticalOffset(PageViewModel page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var offset = 0.0;
        foreach (var p in Pages)
        {
            if (ReferenceEquals(p, page))
            {
                return offset;
            }
            offset += p.Height + ConsecutivePageGap;
        }
        return offset;
    }

    public void UpdateVisiblePagesHydration(double verticalOffset, double viewportHeight)
    {
        if (CurrentViewMode == ViewMode.Separated)
        {
            foreach (var page in Pages)
            {
                if (ReferenceEquals(page, ActivePage))
                {
                    page.Hydrate();
                }
                else
                {
                    page.Dehydrate();
                }
            }
            return;
        }

        var zoom = ZoomLevel > 0.0 ? ZoomLevel : 1.0;
        var viewTop = verticalOffset / zoom;
        var viewBottom = (verticalOffset + viewportHeight) / zoom;
        var cacheMargin = viewportHeight > 0.0 ? viewportHeight / zoom : 1000.0;
        var hydrateTop = viewTop - cacheMargin;
        var hydrateBottom = viewBottom + cacheMargin;

        var currentY = 0.0;
        PageViewModel? mostVisiblePage = null;
        var maxIntersection = -1.0;

        foreach (var page in Pages)
        {
            var pageTop = currentY;
            var pageBottom = pageTop + page.Height;
            var intersectsCache = pageBottom >= hydrateTop && pageTop <= hydrateBottom;

            if (intersectsCache)
            {
                page.Hydrate();
            }
            else
            {
                page.Dehydrate();
            }

            var intersectionStart = Math.Max(viewTop, pageTop);
            var intersectionEnd = Math.Min(viewBottom, pageBottom);
            var intersectionLength = Math.Max(0.0, intersectionEnd - intersectionStart);
            if (intersectionLength > maxIntersection)
            {
                maxIntersection = intersectionLength;
                mostVisiblePage = page;
            }

            currentY += page.Height + ConsecutivePageGap;
        }

        if (mostVisiblePage is not null && !ReferenceEquals(ActivePage, mostVisiblePage) && maxIntersection > 0.0)
        {
            ActivePage = mostVisiblePage;
        }
    }

    partial void OnActivePageChanged(PageViewModel? oldValue, PageViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
            if (CurrentViewMode == ViewMode.Separated)
            {
                oldValue.Dehydrate();
            }
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
            newValue.Hydrate();
            SyncDimensionInputsFromDimensions(new PageDimensions(newValue.Width, newValue.Height));
        }
    }

    partial void OnCurrentViewModeChanged(ViewMode value)
    {
        if (value == ViewMode.Separated)
        {
            foreach (var page in Pages)
            {
                if (ReferenceEquals(page, ActivePage))
                {
                    page.Hydrate();
                }
                else
                {
                    page.Dehydrate();
                }
            }
        }
        else if (ActivePage is not null)
        {
            ActivePage.Hydrate();
            PageNavigationScrollRequested?.Invoke(ActivePage);
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
        var pageVm = new PageViewModel(model, UndoRedo, Toolbar, Document.Assets.GetValueOrDefault, (k, b) => Document.Assets[k] = b);
        Pages.Add(pageVm);
        ActivePage = pageVm;
        NotifyPageCountChanged();
        PageNavigationScrollRequested?.Invoke(pageVm);
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
        PageNavigationScrollRequested?.Invoke(ActivePage);
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
            PageNavigationScrollRequested?.Invoke(ActivePage);
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
            PageNavigationScrollRequested?.Invoke(ActivePage);
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
            Toolbar.IsPenOptionsOpen = false;
            Toolbar.IsEraserOptionsOpen = false;
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

    public Func<string?>? FilePicker { get; set; }

    [RelayCommand]
    public void InsertImage()
    {
        if (ActivePage is null)
        {
            return;
        }

        string? selectedPath = null;
        if (FilePicker is not null)
        {
            selectedPath = FilePicker();
        }
        else
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Insert Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                selectedPath = dialog.FileName;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            InsertImageFile(selectedPath);
        }
    }

    public void InsertImageFile(string filePath)
    {
        if (ActivePage is null || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var result = MediaIngestionService.IngestFile(filePath);
        AddIngestedMediaToActivePage(result);
    }

    public void InsertImageBitmap(BitmapSource bitmap)
    {
        if (ActivePage is null)
        {
            return;
        }

        var result = MediaIngestionService.IngestBitmapSource(bitmap);
        AddIngestedMediaToActivePage(result);
    }

    [RelayCommand]
    public void PasteClipboard()
    {
        if (ActivePage is null)
        {
            return;
        }

        try
        {
            if (Clipboard.ContainsImage())
            {
                var bitmap = Clipboard.GetImage();
                if (bitmap is not null)
                {
                    InsertImageBitmap(bitmap);
                    return;
                }
            }

            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                foreach (string? file in files)
                {
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                    {
                        InsertImageFile(file);
                        return;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Clipboard access may fail if locked by another application
        }
    }

    private void AddIngestedMediaToActivePage(IngestedMediaResult result)
    {
        if (ActivePage is null)
        {
            return;
        }

        Document.Assets[result.AssetKey] = result.SanitizedBytes;

        var maxTargetW = Math.Min(ActivePage.Width * 0.75, 800.0);
        var maxTargetH = Math.Min(ActivePage.Height * 0.75, 800.0);

        var displayW = result.PixelWidth;
        var displayH = result.PixelHeight;

        if (displayW > maxTargetW)
        {
            var scale = maxTargetW / displayW;
            displayW = maxTargetW;
            displayH *= scale;
        }

        if (displayH > maxTargetH)
        {
            var scale = maxTargetH / displayH;
            displayH = maxTargetH;
            displayW *= scale;
        }

        displayW = Math.Max(20.0, displayW);
        displayH = Math.Max(20.0, displayH);

        var x = Math.Max(0.0, (ActivePage.Width - displayW) / 2.0);
        var y = Math.Max(0.0, (ActivePage.Height - displayH) / 2.0);

        ActivePage.AddImageElement(result.AssetKey, result.FrozenBitmap, x, y, displayW, displayH);
    }

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
