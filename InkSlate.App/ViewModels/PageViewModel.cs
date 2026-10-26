using System.IO;
using System.Windows.Ink;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using InkSlate.Core.Models;
using InkSlate.Core.UndoRedo;

namespace InkSlate.App.ViewModels;

public sealed partial class PageViewModel : ObservableObject
{
    public const int MaxStylusPointsPerPage = 100_000;

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

    public PageViewModel(NotePage model, UndoRedoManager? undoRedoManager = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        UndoRedo = undoRedoManager;
        _pageNumber = model.PageNumber;
        _width = model.Width;
        _height = model.Height;
        _backgroundHex = model.BackgroundHex;
    }

    public NotePage Model { get; }
    public UndoRedoManager? UndoRedo { get; }
    public Guid Id => Model.Id;
    public int CompressedIsfByteCount => _compressedIsfBytes?.Length ?? 0;

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
                hydratedStrokes = EnforceStylusPointBudget(loaded);
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
}
