using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkSlate.Core.Ink;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.ViewModels;

public sealed partial class PageViewModel : ObservableObject
{
    public const int MaxStylusPointsPerPage = 100_000;

    private readonly SynchronizedCanvasElementList _synchronizedElements;
    private byte[]? _compressedIsfBytes;

    internal IList<CanvasElement> SynchronizedElements => _synchronizedElements;

    [ObservableProperty]
    private StrokeCollection? _selectedStrokes;

    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private string _backgroundHex;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isHydrated;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private StrokeCollection? _strokes;

    public PageViewModel(
        NotePage model,
        UndoRedoManager? undoRedoManager = null,
        ToolbarViewModel? toolbar = null,
        Func<string, byte[]?>? assetResolver = null,
        Action<string, byte[]>? assetRegistrar = null,
        StrokeCollection? initialStrokes = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        UndoRedo = undoRedoManager;
        Toolbar = toolbar;
        AssetResolver = assetResolver;
        AssetRegistrar = assetRegistrar;
        _pageNumber = model.PageNumber;
        _width = model.Width;
        _height = model.Height;
        _backgroundHex = model.BackgroundHex;

        if (initialStrokes is { Count: > 0 })
        {
            using var ms = new MemoryStream();
            initialStrokes.Save(ms, compress: true);
            _compressedIsfBytes = ms.ToArray();
        }

        _synchronizedElements = new SynchronizedCanvasElementList(this, model.Elements);
        foreach (var element in model.Elements)
        {
            var vm = CanvasElementViewModel.Create(element, AssetResolver);
            ConfigureElementViewModel(vm);
            Elements.Add(vm);
        }

        if (UndoRedo is not null)
        {
            UndoRedo.StateChanged += OnUndoRedoStateChanged;
        }
    }

    public NotePage Model { get; }
    public UndoRedoManager? UndoRedo { get; }
    public ToolbarViewModel? Toolbar { get; }
    public Func<string, byte[]?>? AssetResolver { get; }
    public Action<string, byte[]>? AssetRegistrar { get; }
    public Guid Id => Model.Id;
    public int CompressedIsfByteCount => _compressedIsfBytes?.Length ?? 0;

    public bool IsEmpty
    {
        get
        {
            var hasStrokes = (Strokes?.Count ?? (CompressedIsfByteCount > 0 ? 1 : 0)) > 0;
            if (hasStrokes)
            {
                return false;
            }

            foreach (var el in Elements)
            {
                if (el is TextCanvasElementViewModel textVm)
                {
                    if (!string.IsNullOrWhiteSpace(textVm.Text))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (Model.Elements.Count > 0)
            {
                foreach (var el in Model.Elements)
                {
                    if (el is TextCanvasElement textModel)
                    {
                        if (!string.IsNullOrWhiteSpace(textModel.Text))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public ObservableCollection<CanvasElementViewModel> Elements { get; } = [];

    public void AddElement(CanvasElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element.ZIndex == 0 && Elements.Count > 0)
        {
            if (element is ImageCanvasElement)
            {
                var minZ = Elements.Min(e => e.ZIndex);
                element.ZIndex = minZ - 1;
            }
            else if (element is TextCanvasElement)
            {
                var maxZ = Elements.Max(e => e.ZIndex);
                element.ZIndex = maxZ + 1;
            }
        }

        var vm = CanvasElementViewModel.Create(element, AssetResolver);
        ConfigureElementViewModel(vm);
        Elements.Add(vm);
        Model.Elements.Add(element);
        IsDirty = true;

        if (UndoRedo is not null && !UndoRedo.IsReplaying)
        {
            UndoRedo.Record(new ElementAddedOperation(Id, _synchronizedElements, element));
        }
    }

    public ImageCanvasElementViewModel AddImageElement(
        string assetKey,
        System.Windows.Media.Imaging.BitmapSource frozenBitmap,
        double x,
        double y,
        double width,
        double height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetKey);
        ArgumentNullException.ThrowIfNull(frozenBitmap);

        var model = new ImageCanvasElement
        {
            AssetKey = assetKey,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        if (Elements.Count > 0)
        {
            var minZ = Elements.Min(e => e.ZIndex);
            model.ZIndex = minZ - 1;
        }

        var vm = new ImageCanvasElementViewModel(model)
        {
            ImageSource = frozenBitmap
        };

        ConfigureElementViewModel(vm);
        Elements.Add(vm);
        Model.Elements.Add(model);
        IsDirty = true;

        if (UndoRedo is not null && !UndoRedo.IsReplaying)
        {
            UndoRedo.Record(new ElementAddedOperation(Id, _synchronizedElements, model));
        }

        return vm;
    }

    public void RegisterAsset(string assetKey, byte[] assetBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetKey);
        ArgumentNullException.ThrowIfNull(assetBytes);
        AssetRegistrar?.Invoke(assetKey, assetBytes);
    }

    public TextCanvasElementViewModel AddTextElement(Point position)
    {
        SelectSingleElement(null);

        var model = new TextCanvasElement
        {
            X = Math.Max(0.0, position.X),
            Y = Math.Max(0.0, position.Y),
            Width = 260.0,
            Height = 60.0,
            Text = string.Empty,
            FontSize = Toolbar?.SelectedTextFontSize ?? 18.0,
            IsBold = Toolbar?.IsTextBold ?? false,
            IsItalic = Toolbar?.IsTextItalic ?? false,
            ForegroundHex = Toolbar?.SelectedTextColorHex ?? (Toolbar?.SelectedPenColorHex ?? "#FF1E1E1E")
        };

        AddElement(model);
        var vm = (TextCanvasElementViewModel)Elements.First(e => ReferenceEquals(e.Model, model));
        vm.IsSelected = true;
        vm.IsEditing = true;

        if (Toolbar is not null)
        {
            Toolbar.ActiveTool = ToolMode.Pointer;
            Toolbar.IsTextOptionsOpen = false;
        }

        return vm;
    }

    [RelayCommand]
    public void BringElementToFront(CanvasElementViewModel? elementVm)
    {
        if (elementVm is null || !Elements.Contains(elementVm) || Elements.Count <= 1)
        {
            return;
        }

        var maxZ = Elements.Max(e => e.ZIndex);
        if (elementVm.ZIndex > maxZ || (elementVm.ZIndex == maxZ && Elements.Count(e => e.ZIndex == maxZ) == 1))
        {
            return;
        }

        var oldZ = elementVm.ZIndex;
        var newZ = maxZ + 1;
        elementVm.ZIndex = newZ;
        IsDirty = true;

        if (UndoRedo is not null && !UndoRedo.IsReplaying)
        {
            UndoRedo.Record(new ElementZIndexChangedOperation(Id, [new ElementZIndexChange(elementVm.Model, oldZ, newZ)]));
        }
    }

    [RelayCommand]
    public void SendElementToBack(CanvasElementViewModel? elementVm)
    {
        if (elementVm is null || !Elements.Contains(elementVm) || Elements.Count <= 1)
        {
            return;
        }

        var minZ = Elements.Min(e => e.ZIndex);
        if (elementVm.ZIndex < minZ || (elementVm.ZIndex == minZ && Elements.Count(e => e.ZIndex == minZ) == 1))
        {
            return;
        }

        var oldZ = elementVm.ZIndex;
        var newZ = minZ - 1;
        elementVm.ZIndex = newZ;
        IsDirty = true;

        if (UndoRedo is not null && !UndoRedo.IsReplaying)
        {
            UndoRedo.Record(new ElementZIndexChangedOperation(Id, [new ElementZIndexChange(elementVm.Model, oldZ, newZ)]));
        }
    }

    [RelayCommand]
    public void RemoveElement(CanvasElementViewModel? elementVm)
    {
        if (elementVm is null)
        {
            return;
        }

        var index = Elements.IndexOf(elementVm);
        if (index < 0)
        {
            return;
        }

        elementVm.PropertyChanged -= OnElementPropertyChanged;
        elementVm.RequestDelete = null;
        Elements.RemoveAt(index);
        Model.Elements.Remove(elementVm.Model);
        IsDirty = true;

        if (UndoRedo is not null && !UndoRedo.IsReplaying)
        {
            UndoRedo.Record(new ElementRemovedOperation(Id, _synchronizedElements, elementVm.Model, index));
        }
    }

    [RelayCommand]
    public void DeleteSelectedElements()
    {
        var selectedElements = Elements.Where(e => e.IsSelected && !e.IsEditing).ToList();
        foreach (var el in selectedElements)
        {
            RemoveElement(el);
        }
    }

    public void ClearSelection()
    {
        SelectSingleElement(null);
        SelectedStrokes?.Clear();
        SelectedStrokes = null;
    }

    public void SelectSingleElement(CanvasElementViewModel? target)
    {
        var elementsSnapshot = Elements.ToList();
        foreach (var el in elementsSnapshot)
        {
            if (!ReferenceEquals(el, target))
            {
                el.IsSelected = false;
                el.IsEditing = false;
            }
        }

        CleanupEmptyTextElements();
    }

    public void CleanupEmptyTextElements()
    {
        var emptyTexts = Elements.OfType<TextCanvasElementViewModel>()
            .Where(t => !t.IsEditing && string.IsNullOrWhiteSpace(t.Text))
            .ToList();

        foreach (var empty in emptyTexts)
        {
            RemoveElement(empty);
        }
    }

    private void OnElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is TextCanvasElementViewModel textVm && e.PropertyName == nameof(TextCanvasElementViewModel.IsEditing))
        {
            if (!textVm.IsEditing && string.IsNullOrWhiteSpace(textVm.Text))
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is not null && !dispatcher.CheckAccess())
                {
                    dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
                    {
                        if (!textVm.IsEditing && string.IsNullOrWhiteSpace(textVm.Text))
                        {
                            RemoveElement(textVm);
                        }
                    });
                }
                else
                {
                    RemoveElement(textVm);
                }
            }
        }
    }

    public void LoadIsfBytes(byte[]? isfBytes)
    {
        _compressedIsfBytes = isfBytes is { Length: > 0 } ? [.. isfBytes] : null;
        IsDirty = false;

        if (IsHydrated)
        {
            Dehydrate();
            Hydrate();
        }
    }

    public void LoadStrokes(StrokeCollection? strokes)
    {
        if (strokes is null || strokes.Count == 0)
        {
            LoadIsfBytes(null);
            return;
        }

        using var ms = new MemoryStream();
        strokes.Save(ms, compress: true);
        LoadIsfBytes(ms.ToArray());
    }

    public byte[]? ExportIsfBytes()
    {
        if (IsHydrated && Strokes is not null)
        {
            if (Strokes.Count == 0)
            {
                return null;
            }

            using var ms = new MemoryStream();
            Strokes.Save(ms, compress: true);
            _compressedIsfBytes = ms.ToArray();
            IsDirty = false;
        }

        return _compressedIsfBytes is { Length: > 0 } ? [.. _compressedIsfBytes] : null;
    }

    public void Hydrate()
    {
        if (IsHydrated && Strokes is not null)
        {
            return;
        }

        StrokeCollection hydratedStrokes;
        if (_compressedIsfBytes is { Length: > 0 })
        {
            try
            {
                using var ms = new MemoryStream(_compressedIsfBytes, writable: false);
                var loaded = new StrokeCollection(ms);
                var split = StrokeSplitter.SplitAll(loaded);
                hydratedStrokes = EnforceStylusPointBudget(split);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                hydratedStrokes = [];
            }
        }
        else
        {
            hydratedStrokes = [];
        }

        hydratedStrokes.StrokesChanged += OnStrokesChanged;
        Strokes = hydratedStrokes;

        foreach (var element in Elements)
        {
            if (element is ImageCanvasElementViewModel imageVm && imageVm.ImageSource is null)
            {
                imageVm.ResolveImage(AssetResolver);
            }
        }

        IsHydrated = true;
    }

    public void Dehydrate()
    {
        if (!IsHydrated || Strokes is null)
        {
            return;
        }

        Strokes.StrokesChanged -= OnStrokesChanged;

        if (IsDirty || _compressedIsfBytes is null)
        {
            if (Strokes.Count == 0)
            {
                _compressedIsfBytes = null;
            }
            else
            {
                using var ms = new MemoryStream();
                Strokes.Save(ms, compress: true);
                _compressedIsfBytes = ms.ToArray();
            }

            IsDirty = false;
        }

        Strokes = null;
        IsHydrated = false;
    }

    private void OnStrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        IsDirty = true;
    }

    private void OnUndoRedoStateChanged(object? sender, EventArgs e)
    {
        foreach (var element in Elements)
        {
            element.RefreshFromModel();
        }
    }

    private void ConfigureElementViewModel(CanvasElementViewModel vm)
    {
        vm.PropertyChanged += OnElementPropertyChanged;
        vm.RequestDelete = RemoveElement;
    }

    private void OnSynchronizedElementInserted(int index, CanvasElement item)
    {
        IsDirty = true;
        for (var i = 0; i < Elements.Count; i++)
        {
            if (ReferenceEquals(Elements[i].Model, item))
            {
                return;
            }
        }

        var insertIndex = Math.Clamp(index, 0, Elements.Count);
        var vm = CanvasElementViewModel.Create(item, AssetResolver);
        ConfigureElementViewModel(vm);
        Elements.Insert(insertIndex, vm);
    }

    private void OnSynchronizedElementRemoved(CanvasElement item)
    {
        IsDirty = true;
        for (var i = 0; i < Elements.Count; i++)
        {
            if (ReferenceEquals(Elements[i].Model, item))
            {
                Elements.RemoveAt(i);
                break;
            }
        }
    }

    private void OnSynchronizedElementsCleared()
    {
        IsDirty = true;
        Elements.Clear();
    }

    private static StrokeCollection EnforceStylusPointBudget(StrokeCollection source)
    {
        var result = new StrokeCollection();
        var totalPoints = 0;

        foreach (var stroke in source)
        {
            var pointCount = stroke.StylusPoints.Count;
            if (totalPoints + pointCount > MaxStylusPointsPerPage)
            {
                var remaining = MaxStylusPointsPerPage - totalPoints;
                if (remaining >= 2)
                {
                    var truncatedPoints = new StylusPointCollection(remaining);
                    for (var i = 0; i < remaining; i++)
                    {
                        truncatedPoints.Add(stroke.StylusPoints[i]);
                    }

                    result.Add(new Stroke(truncatedPoints, stroke.DrawingAttributes.Clone()));
                }

                break;
            }

            result.Add(stroke);
            totalPoints += pointCount;
        }

        return result;
    }

    partial void OnPageNumberChanged(int value) => Model.PageNumber = value;
    partial void OnWidthChanged(double value) => Model.Width = value;
    partial void OnHeightChanged(double value) => Model.Height = value;
    partial void OnBackgroundHexChanged(string value) => Model.BackgroundHex = value;

    private sealed class SynchronizedCanvasElementList : IList<CanvasElement>
    {
        private readonly PageViewModel _owner;
        private readonly IList<CanvasElement> _inner;

        public SynchronizedCanvasElementList(PageViewModel owner, IList<CanvasElement> inner)
        {
            _owner = owner;
            _inner = inner;
        }

        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;

        public CanvasElement this[int index]
        {
            get => _inner[index];
            set
            {
                _inner[index] = value;
                _owner.OnSynchronizedElementRemoved(_inner[index]);
                _owner.OnSynchronizedElementInserted(index, value);
            }
        }

        public void Add(CanvasElement item)
        {
            _inner.Add(item);
            _owner.OnSynchronizedElementInserted(_inner.Count - 1, item);
        }

        public void Clear()
        {
            _inner.Clear();
            _owner.OnSynchronizedElementsCleared();
        }

        public bool Contains(CanvasElement item) => _inner.Contains(item);

        public void CopyTo(CanvasElement[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

        public IEnumerator<CanvasElement> GetEnumerator() => _inner.GetEnumerator();

        public int IndexOf(CanvasElement item) => _inner.IndexOf(item);

        public void Insert(int index, CanvasElement item)
        {
            _inner.Insert(index, item);
            _owner.OnSynchronizedElementInserted(index, item);
        }

        public bool Remove(CanvasElement item)
        {
            var removed = _inner.Remove(item);
            if (removed)
            {
                _owner.OnSynchronizedElementRemoved(item);
            }
            return removed;
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < _inner.Count)
            {
                var item = _inner[index];
                _inner.RemoveAt(index);
                _owner.OnSynchronizedElementRemoved(item);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
