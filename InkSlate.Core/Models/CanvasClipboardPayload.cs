using System.IO;
using System.Text.Json.Serialization;
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

