using System.IO;
using System.Text;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using SkiaSharp;

namespace InkSlate.Core.Services;

/// <summary>
/// Represents the progress of a PDF export operation.
/// </summary>
/// <param name="CompletedPages">The number of pages fully exported so far.</param>
/// <param name="TotalPages">The total number of pages to export.</param>
public readonly record struct PdfExportProgress(int CompletedPages, int TotalPages)
{
    /// <summary>
    /// Gets the completion percentage clamped between 0.0 and 100.0.
    /// If <see cref="TotalPages"/> is zero, returns 100.0.
    /// </summary>
    public double Percentage => TotalPages > 0 ? Math.Clamp((double)CompletedPages / TotalPages * 100.0, 0.0, 100.0) : 100.0;
}

/// <summary>
/// High-performance SkiaSharp vector PDF export engine.
/// Renders NoteDocument pages with 1:1 DIP-to-point coordinate alignment, non-destructive image cropping,
/// wrapped vector text glyphs, and smooth cubic Bézier vector ink paths.
/// </summary>
public static class PdfExportService
{
    public const float DefaultDpi = 96f;

    /// <summary>
    /// Synchronously exports the specified page indices of a <see cref="NoteDocument"/> into a vector PDF stream.
    /// </summary>
    public static void Export(
        NoteDocument doc,
        IReadOnlyList<int> pageIndices,
        Stream outputStream,
        Func<Guid, StrokeCollection?>? strokesProvider = null,
        IProgress<PdfExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(pageIndices);
        ArgumentNullException.ThrowIfNull(outputStream);

        using var pdfDoc = SKDocument.CreatePdf(outputStream, DefaultDpi);
        if (pdfDoc is null)
        {
            throw new InvalidOperationException("Failed to initialize SkiaSharp PDF document.");
        }

        var totalPages = pageIndices.Count;
        var completedPages = 0;
        progress?.Report(new PdfExportProgress(0, totalPages));

        foreach (var pageIndex in pageIndices)
        {
            ct.ThrowIfCancellationRequested();

            if (pageIndex < 0 || pageIndex >= doc.Pages.Count)
            {
                completedPages++;
                progress?.Report(new PdfExportProgress(completedPages, totalPages));
                continue;
            }

            var page = doc.Pages[pageIndex];
            using var pdfCanvas = pdfDoc.BeginPage((float)page.Width, (float)page.Height);

            // 1. Draw page background
            var bg = SKColor.TryParse(page.BackgroundHex, out var parsedBg) ? parsedBg : SKColors.White;
            pdfCanvas.Clear(bg);

            // 2. Render canvas elements sorted by Z-Index
            var sortedElements = page.Elements.OrderBy(static e => e.ZIndex);
            foreach (var element in sortedElements)
            {
                ct.ThrowIfCancellationRequested();

                switch (element)
                {
                    case ImageCanvasElement imgEl:
                        RenderImageElement(pdfCanvas, imgEl, doc);
                        break;

                    case TextCanvasElement textEl:
                        RenderTextElement(pdfCanvas, textEl);
                        break;
                }
            }

            // 3. Render vector ink strokes on top
            var strokes = strokesProvider?.Invoke(page.Id) ?? doc.PageStrokes.GetValueOrDefault(page.Id);
            if (strokes is { Count: > 0 })
            {
                foreach (var stroke in strokes)
                {
                    ct.ThrowIfCancellationRequested();
                    RenderStroke(pdfCanvas, stroke);
                }
            }

            pdfDoc.EndPage();
            completedPages++;
            progress?.Report(new PdfExportProgress(completedPages, totalPages));
        }

        pdfDoc.Close();
    }

    /// <summary>
    /// Asynchronously exports the specified page indices on a background thread (<see cref="Task.Run(Action)"/>)
    /// to avoid blocking the UI dispatcher.
    /// </summary>
    public static Task ExportAsync(
        NoteDocument doc,
        IReadOnlyList<int> pageIndices,
        Stream outputStream,
        Func<Guid, StrokeCollection?>? strokesProvider = null,
        IProgress<PdfExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(pageIndices);
        ArgumentNullException.ThrowIfNull(outputStream);

        return Task.Run(() => Export(doc, pageIndices, outputStream, strokesProvider, progress, ct), ct);
    }

    /// <summary>
    /// Atomically exports the specified page indices into a target file path,
    /// writing to a temporary sibling file and committing via atomic move upon completion.
    /// </summary>
    public static async Task ExportToFileAsync(
        NoteDocument doc,
        IReadOnlyList<int> pageIndices,
        string targetPath,
        Func<Guid, StrokeCollection?>? strokesProvider = null,
        IProgress<PdfExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(pageIndices);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var targetFullPath = Path.GetFullPath(targetPath);
        var directory = Path.GetDirectoryName(targetFullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{targetFullPath}.tmp.{Guid.NewGuid():N}";

        try
        {
            await Task.Run(() =>
            {
                using var fs = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024);

                Export(doc, pageIndices, fs, strokesProvider, progress, ct);
            }, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, targetFullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore temp file cleanup errors
                }
            }
            throw;
        }
    }

    private static void RenderImageElement(SKCanvas canvas, ImageCanvasElement img, NoteDocument doc)
    {
        if (string.IsNullOrWhiteSpace(img.AssetKey) ||
            !doc.Assets.TryGetValue(img.AssetKey, out var imageBytes) ||
            imageBytes is not { Length: > 0 })
        {
            return;
        }

        try
        {
            using var skImage = SKImage.FromEncodedData(imageBytes);
            if (skImage is null)
            {
                return;
            }

            var destRect = new SKRect((float)img.X, (float)img.Y, (float)(img.X + img.Width), (float)(img.Y + img.Height));

            SKRect srcRect;
            if (img.CropWidth > 0 && img.CropHeight > 0)
            {
                srcRect = new SKRect(
                    (float)img.CropX,
                    (float)img.CropY,
                    (float)(img.CropX + img.CropWidth),
                    (float)(img.CropY + img.CropHeight));
            }
            else
            {
                srcRect = new SKRect(0, 0, skImage.Width, skImage.Height);
            }

            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            canvas.DrawImage(skImage, srcRect, destRect, sampling, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Corrupted image data is safely ignored
        }
    }

    private static void RenderTextElement(SKCanvas canvas, TextCanvasElement textEl)
    {
        if (string.IsNullOrWhiteSpace(textEl.Text))
        {
            return;
        }

        var weight = textEl.IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = textEl.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var width = SKFontStyleWidth.Normal;

        using var typeface = SKTypeface.FromFamilyName(
            string.IsNullOrWhiteSpace(textEl.FontFamily) ? "Segoe UI" : textEl.FontFamily,
            weight,
            width,
            slant) ?? SKTypeface.Default;

        using var skFont = new SKFont(typeface, (float)Math.Max(1.0, textEl.FontSize));
        var textColor = SKColor.TryParse(textEl.ForegroundHex, out var parsed) ? parsed : SKColors.Black;
        using var skPaint = new SKPaint { Color = textColor, IsAntialias = true };

        skFont.GetFontMetrics(out var metrics);
        var lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
        if (lineHeight <= 0)
        {
            lineHeight = (float)textEl.FontSize * 1.2f;
        }

        var maxLineWidth = (float)Math.Max(10.0, textEl.Width);
        var rawLines = textEl.Text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var linesToDraw = new List<string>();

        foreach (var rawLine in rawLines)
        {
            if (string.IsNullOrEmpty(rawLine))
            {
                linesToDraw.Add(string.Empty);
                continue;
            }

            var words = rawLine.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var testLine = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                var measuredWidth = skFont.MeasureText(testLine);

                if (measuredWidth > maxLineWidth && currentLine.Length > 0)
                {
                    linesToDraw.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                    {
                        currentLine.Append(' ');
                    }
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
            {
                linesToDraw.Add(currentLine.ToString());
            }
        }

        var y = (float)textEl.Y - metrics.Ascent;
        var maxY = (float)(textEl.Y + Math.Max(textEl.Height, lineHeight));

        foreach (var line in linesToDraw)
        {
            if (y - metrics.Ascent > maxY + lineHeight * 2)
            {
                break;
            }

            if (!string.IsNullOrEmpty(line))
            {
                canvas.DrawText(line, (float)textEl.X, y, SKTextAlign.Left, skFont, skPaint);
            }

            y += lineHeight;
        }
    }

    private static void RenderStroke(SKCanvas canvas, Stroke stroke)
    {
        var pts = stroke.StylusPoints;
        if (pts.Count == 0)
        {
            return;
        }

        var da = stroke.DrawingAttributes;
        var isHighlighter = da.IsHighlighter;
        var color = ToSkColor(da.Color, isHighlighter);
        var strokeWidth = (float)Math.Max(1.0, Math.Max(da.Width, da.Height));
        var strokeCap = da.StylusTip == StylusTip.Rectangle ? SKStrokeCap.Square : SKStrokeCap.Round;

        using var skPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = strokeWidth,
            StrokeCap = strokeCap,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
            BlendMode = isHighlighter ? SKBlendMode.Multiply : SKBlendMode.SrcOver
        };

        if (da.FitToCurve && pts.Count >= 2)
        {
            var bezierPoints = stroke.GetBezierStylusPoints();
            if (bezierPoints.Count >= 4)
            {
                using var skPath = new SKPath();
                skPath.MoveTo((float)bezierPoints[0].X, (float)bezierPoints[0].Y);

                for (var i = 1; i + 2 < bezierPoints.Count; i += 3)
                {
                    skPath.CubicTo(
                        (float)bezierPoints[i].X, (float)bezierPoints[i].Y,
                        (float)bezierPoints[i + 1].X, (float)bezierPoints[i + 1].Y,
                        (float)bezierPoints[i + 2].X, (float)bezierPoints[i + 2].Y);
                }

                canvas.DrawPath(skPath, skPaint);
                return;
            }
        }

        if (pts.Count == 1)
        {
            canvas.DrawPoint((float)pts[0].X, (float)pts[0].Y, skPaint);
        }
        else
        {
            using var skPath = new SKPath();
            skPath.MoveTo((float)pts[0].X, (float)pts[0].Y);

            for (var i = 1; i < pts.Count; i++)
            {
                skPath.LineTo((float)pts[i].X, (float)pts[i].Y);
            }

            canvas.DrawPath(skPath, skPaint);
        }
    }

    private static SKColor ToSkColor(System.Windows.Media.Color color, bool isHighlighter)
    {
        if (isHighlighter)
        {
            return new SKColor(color.R, color.G, color.B, 110);
        }

        return new SKColor(color.R, color.G, color.B, color.A);
    }
}

