using System.IO;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.Models;

public sealed class CanvasClipboardPayload
{
    public List<CanvasElement> Elements { get; set; } = [];
    public byte[]? StrokeBytes { get; set; }
    public Dictionary<string, byte[]> AssetBlobs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsEmpty => Elements.Count == 0 && (StrokeBytes is null || StrokeBytes.Length == 0);

    public static byte[] SerializeStrokesToBytes(StrokeCollection strokes)
    {
        if (strokes is null || strokes.Count == 0)
        {
            return [];
        }

        using var ms = new MemoryStream();
        strokes.Save(ms, compress: true);
        return ms.ToArray();
    }

    public static StrokeCollection DeserializeStrokesFromBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return [];
        }

        using var ms = new MemoryStream(bytes, writable: false);
        return new StrokeCollection(ms);
    }

    public (List<CanvasElement> ClonedElements, StrokeCollection ClonedStrokes) CreateClonesWithOffset(double offsetX, double offsetY)
    {
        var clonedElements = new List<CanvasElement>(Elements.Count);
        foreach (var el in Elements)
        {
            clonedElements.Add(CloneElement(el, offsetX, offsetY));
        }

        var clonedStrokes = DeserializeStrokesFromBytes(StrokeBytes);
        if (clonedStrokes.Count > 0 && (Math.Abs(offsetX) > 0.001 || Math.Abs(offsetY) > 0.001))
        {
            var matrix = new Matrix();
            matrix.Translate(offsetX, offsetY);
            foreach (var stroke in clonedStrokes)
            {
                stroke.Transform(matrix, false);
            }
        }

        return (clonedElements, clonedStrokes);
    }

    public Rect GetBounds()
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var el in Elements)
        {
            minX = Math.Min(minX, el.X);
            minY = Math.Min(minY, el.Y);
            maxX = Math.Max(maxX, el.X + Math.Max(0.0, el.Width));
            maxY = Math.Max(maxY, el.Y + Math.Max(0.0, el.Height));
        }

        var strokes = DeserializeStrokesFromBytes(StrokeBytes);
        if (strokes.Count > 0)
        {
            var bounds = strokes.GetBounds();
            if (!bounds.IsEmpty)
            {
                minX = Math.Min(minX, bounds.Left);
                minY = Math.Min(minY, bounds.Top);
                maxX = Math.Max(maxX, bounds.Right);
                maxY = Math.Max(maxY, bounds.Bottom);
            }
        }

        if (double.IsInfinity(minX) || double.IsInfinity(minY))
        {
            return Rect.Empty;
        }

        return new Rect(minX, minY, Math.Max(0.0, maxX - minX), Math.Max(0.0, maxY - minY));
    }

    public (List<CanvasElement> ClonedElements, StrokeCollection ClonedStrokes) CreateClonesAtPosition(Point targetPosition)
    {
        var bounds = GetBounds();
        if (bounds.IsEmpty)
        {
            return CreateClonesWithOffset(0, 0);
        }

        var clampedX = Math.Max(0.0, targetPosition.X);
        var clampedY = Math.Max(0.0, targetPosition.Y);

        var offsetX = clampedX - bounds.Left;
        var offsetY = clampedY - bounds.Top;
        return CreateClonesWithOffset(offsetX, offsetY);
    }

    public static CanvasElement CloneElement(CanvasElement source, double offsetX, double offsetY)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is TextCanvasElement text)
        {
            return new TextCanvasElement
            {
                Id = Guid.CreateVersion7(),
                X = text.X + offsetX,
                Y = text.Y + offsetY,
                Width = text.Width,
                Height = text.Height,
                ZIndex = text.ZIndex,
                Text = text.Text,
                FontFamily = text.FontFamily,
                FontSize = text.FontSize,
                ForegroundHex = text.ForegroundHex,
                IsBold = text.IsBold,
                IsItalic = text.IsItalic
            };
        }

        if (source is ImageCanvasElement img)
        {
            return new ImageCanvasElement
            {
                Id = Guid.CreateVersion7(),
                X = img.X + offsetX,
                Y = img.Y + offsetY,
                Width = img.Width,
                Height = img.Height,
                ZIndex = img.ZIndex,
                AssetKey = img.AssetKey,
                CropX = img.CropX,
                CropY = img.CropY,
                CropWidth = img.CropWidth,
                CropHeight = img.CropHeight
            };
        }

        throw new NotSupportedException($"Unsupported canvas element type: {source.GetType().Name}");
    }
}

