using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
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

    public PageViewModel(NotePage model, UndoRedoManager? undoRedoManager = null, ToolbarViewModel? toolbar = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        UndoRedo = undoRedoManager;
        Toolbar = toolbar;
        _pageNumber = model.PageNumber;
        _width = model.Width;
        _height = model.Height;
        _backgroundHex = model.BackgroundHex;

        _synchronizedElements = new SynchronizedCanvasElementList(this, model.Elements);
        foreach (var element in model.Elements)
        {
            Elements.Add(new CanvasElementViewModel(element));
        }
    }

    public NotePage Model { get; }
    public UndoRedoManager? UndoRedo { get; }
    public ToolbarViewModel? Toolbar { get; }
    public Guid Id => Model.Id;
    public int CompressedIsfByteCount => _compressedIsfBytes?.Length ?? 0;

    public ObservableCollection<CanvasElementViewModel> Elements { get; } = [];

    public void AddElement(CanvasElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var vm = new CanvasElementViewModel(element);
        Elements.Add(vm);
        Model.Elements.Add(element);
        IsDirty = true;

        if (UndoRedo is not null && !UndoRedo.IsReplaying)
        {
            UndoRedo.Record(new ElementAddedOperation(Id, _synchronizedElements, element));
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

        Elements.RemoveAt(index);
        Model.Elements.Remove(elementVm.Model);
        IsDirty = true;

        if (UndoRedo is not null && !UndoRedo.IsReplaying)
        {
            UndoRedo.Record(new ElementRemovedOperation(Id, _synchronizedElements, elementVm.Model, index));
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
        Elements.Insert(insertIndex, new CanvasElementViewModel(item));
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
