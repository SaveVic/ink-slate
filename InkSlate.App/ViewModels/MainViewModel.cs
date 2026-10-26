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
    [NotifyPropertyChangedFor(nameof(IsA4Selected))]
    [NotifyPropertyChangedFor(nameof(IsLetterSelected))]
    [NotifyPropertyChangedFor(nameof(IsLegalSelected))]
    [NotifyPropertyChangedFor(nameof(IsSlide16x9Selected))]
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

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private string _documentTitle = NoteDocument.DefaultTitle;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPdfExportAll))]
    [NotifyPropertyChangedFor(nameof(IsPdfExportCurrent))]
    [NotifyPropertyChangedFor(nameof(IsPdfExportRange))]
    private PdfExportPageMode _pdfExportMode = PdfExportPageMode.AllPages;

    [ObservableProperty]
    private string _pdfCustomRangeInput = string.Empty;

    [ObservableProperty]
    private bool _hasPdfRangeError;

    [ObservableProperty]
    private bool _isPdfExportOpen;

    [ObservableProperty]
    private bool _isExportingPdf;

    public bool IsPdfExportAll => PdfExportMode == PdfExportPageMode.AllPages;
    public bool IsPdfExportCurrent => PdfExportMode == PdfExportPageMode.CurrentPage;
    public bool IsPdfExportRange => PdfExportMode == PdfExportPageMode.CustomRange;

    public bool IsA4Selected => SelectedPreset == PageDimensionPreset.A4;
    public bool IsLetterSelected => SelectedPreset == PageDimensionPreset.Letter;
    public bool IsLegalSelected => SelectedPreset == PageDimensionPreset.Legal;
    public bool IsSlide16x9Selected => SelectedPreset == PageDimensionPreset.Slide16x9;

    public Func<string?>? FileOpenPicker { get; set; }
    public Func<string?>? FileSavePicker { get; set; }
    public Func<string?>? FilePdfPicker { get; set; }

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
                (e.PropertyName == nameof(Toolbar.IsEraserOptionsOpen) && Toolbar.IsEraserOptionsOpen) ||
                (e.PropertyName == nameof(Toolbar.IsLassoOptionsOpen) && Toolbar.IsLassoOptionsOpen) ||
                (e.PropertyName == nameof(Toolbar.IsTextOptionsOpen) && Toolbar.IsTextOptionsOpen))
            {
                IsPageSetupOpen = false;
                IsPdfExportOpen = false;
            }

            if (e.PropertyName is nameof(Toolbar.SelectedTextFontSize) or
                nameof(Toolbar.IsTextBold) or
                nameof(Toolbar.IsTextItalic) or
                nameof(Toolbar.SelectedTextColorHex))
            {
                var activeText = ActivePage?.Elements.OfType<TextCanvasElementViewModel>()
                    .FirstOrDefault(t => t.IsSelected || t.IsEditing);
                if (activeText is not null)
                {
                    activeText.FontSize = Toolbar.SelectedTextFontSize;
                    activeText.IsBold = Toolbar.IsTextBold;
                    activeText.IsItalic = Toolbar.IsTextItalic;
                    activeText.ForegroundHex = Toolbar.SelectedTextColorHex;
                }
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

    public NoteDocument Document { get; private set; }
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
            Toolbar.IsLassoOptionsOpen = false;
            Toolbar.IsTextOptionsOpen = false;
            IsPdfExportOpen = false;
        }

        IsPageSetupOpen = !IsPageSetupOpen;
    }

    [RelayCommand]
    private void TogglePdfExport()
    {
        if (!IsPdfExportOpen)
        {
            Toolbar.IsPenOptionsOpen = false;
            Toolbar.IsEraserOptionsOpen = false;
            Toolbar.IsLassoOptionsOpen = false;
            Toolbar.IsTextOptionsOpen = false;
            IsPageSetupOpen = false;
            HasPdfRangeError = false;
        }

        IsPdfExportOpen = !IsPdfExportOpen;
    }

    [RelayCommand]
    private void SelectPdfExportMode(PdfExportPageMode mode)
    {
        PdfExportMode = mode;
        HasPdfRangeError = false;
    }

    partial void OnPdfCustomRangeInputChanged(string value)
    {
        if (HasPdfRangeError)
        {
            HasPdfRangeError = false;
        }
    }

    [RelayCommand]
    public async Task ExecutePdfExportAsync()
    {
        if (IsExportingPdf || Pages.Count == 0)
        {
            return;
        }

        var activeIndex = ActivePageIndex - 1;
        if (!PageRangeParser.TryResolvePageIndices(PdfExportMode, Pages.Count, activeIndex, PdfCustomRangeInput, out var targetIndices))
        {
            HasPdfRangeError = true;
            return;
        }

        HasPdfRangeError = false;

        string? selectedPath = null;
        if (FilePdfPicker is not null)
        {
            selectedPath = FilePdfPicker();
        }
        else
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export PDF Document",
                Filter = "PDF Document (*.pdf)|*.pdf|All Files (*.*)|*.*",
                DefaultExt = ".pdf",
                FileName = $"{DocumentTitle}.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedPath = dialog.FileName;
            }
        }

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        try
        {
            IsExportingPdf = true;
            await PdfExportService.ExportToFileAsync(
                Document,
                targetIndices,
                selectedPath,
                strokesProvider: id => Pages.FirstOrDefault(p => p.Id == id)?.Strokes);

            IsPdfExportOpen = false;
        }
        finally
        {
            IsExportingPdf = false;
        }
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

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            await SaveAsAsync();
        }
        else
        {
            await SaveToFileAsync(CurrentFilePath);
        }
    }

    [RelayCommand]
    public async Task SaveAsAsync()
    {
        string? selectedPath = null;
        if (FileSavePicker is not null)
        {
            selectedPath = FileSavePicker();
        }
        else
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save InkSlate Document",
                Filter = "InkSlate Document (*.notex)|*.notex|All Files (*.*)|*.*",
                DefaultExt = ".notex",
                FileName = !string.IsNullOrWhiteSpace(CurrentFilePath)
                    ? Path.GetFileName(CurrentFilePath)
                    : $"{DocumentTitle}.notex"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedPath = dialog.FileName;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            await SaveToFileAsync(selectedPath);
        }
    }

    [RelayCommand]
    public async Task OpenAsync()
    {
        string? selectedPath = null;
        if (FileOpenPicker is not null)
        {
            selectedPath = FileOpenPicker();
        }
        else
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open InkSlate Document",
                Filter = "InkSlate Document (*.notex)|*.notex|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                selectedPath = dialog.FileName;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            await LoadFromFileAsync(selectedPath);
        }
    }

    public async Task SaveToFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (IsSaving)
        {
            return;
        }

        try
        {
            IsSaving = true;
            await FilePackageService.SaveAsync(
                Document,
                filePath,
                strokesProvider: id => Pages.FirstOrDefault(p => p.Id == id)?.Strokes,
                isfBytesProvider: id => Pages.FirstOrDefault(p => p.Id == id)?.ExportIsfBytes());

            foreach (var page in Pages)
            {
                page.IsDirty = false;
            }

            CurrentFilePath = filePath;
            DocumentTitle = Path.GetFileNameWithoutExtension(filePath);
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task LoadFromFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var loadedDoc = await FilePackageService.LoadAsync(filePath);
            LoadDocument(loadedDoc, filePath);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void LoadDocument(NoteDocument loadedDoc, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(loadedDoc);

        Document = loadedDoc;
        DocumentTitle = !string.IsNullOrWhiteSpace(loadedDoc.Title)
            ? loadedDoc.Title
            : (!string.IsNullOrWhiteSpace(filePath) ? Path.GetFileNameWithoutExtension(filePath) : NoteDocument.DefaultTitle);
        CurrentFilePath = filePath;

        Pages.Clear();
        foreach (var page in loadedDoc.Pages)
        {
            var strokes = loadedDoc.PageStrokes.GetValueOrDefault(page.Id);
            var pageVm = new PageViewModel(
                page,
                UndoRedo,
                Toolbar,
                Document.Assets.GetValueOrDefault,
                (k, b) => Document.Assets[k] = b,
                strokes);
            Pages.Add(pageVm);
        }

        if (Pages.Count == 0)
        {
            var fallbackPage = new NotePage
            {
                PageNumber = 1,
                Width = Document.DefaultPageWidth,
                Height = Document.DefaultPageHeight
            };
            Document.Pages.Add(fallbackPage);
            Pages.Add(new PageViewModel(
                fallbackPage,
                UndoRedo,
                Toolbar,
                Document.Assets.GetValueOrDefault,
                (k, b) => Document.Assets[k] = b));
        }

        UndoRedo.Clear();
        ActivePage = Pages[0];
        ActivePage.Hydrate();
        SyncDimensionInputsFromDimensions(new PageDimensions(ActivePage.Width, ActivePage.Height));
        NotifyPageCountChanged();
    }
}
