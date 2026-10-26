using System.Text.RegularExpressions;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.Serialization;

public static partial class DocumentValidator
{
    public const double MinElementDimensionDip = 8.0;
    public const double MaxElementDimensionDip = 10000.0;
    public const double MinElementCoordinateDip = -5000.0;
    public const double MaxElementCoordinateDip = 15000.0;
    public const double MinFontSize = 6.0;
    public const double MaxFontSize = 200.0;
    public const int MaxTitleLength = 256;
    public const int MaxFontFamilyLength = 128;
    public const int MaxTextLength = 100_000;

    [GeneratedRegex(@"^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();

    [GeneratedRegex(@"^pages/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.isf$", RegexOptions.CultureInvariant)]
    private static partial Regex IsfEntryPathRegex();

    [GeneratedRegex(@"^assets/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.(?:png|jpg|jpeg)$", RegexOptions.CultureInvariant)]
    private static partial Regex AssetKeyRegex();

    public static bool IsValidHexColor(string? value) =>
        !string.IsNullOrEmpty(value) && HexColorRegex().IsMatch(value);

    public static bool IsValidIsfEntryPath(string? path) =>
        !string.IsNullOrEmpty(path) && IsfEntryPathRegex().IsMatch(path);

    public static bool IsValidAssetKey(string? key) =>
        !string.IsNullOrEmpty(key) && AssetKeyRegex().IsMatch(key);

    /// <summary>
    /// Sanitizes and clamps a deserialized <see cref="NoteDocument"/> in-place so all dimensions,
    /// coordinates, colors, and archive paths satisfy strict safety invariants.
    /// </summary>
    public static NoteDocument Sanitize(NoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Id == Guid.Empty)
        {
            document.Id = Guid.CreateVersion7();
        }

        if (document.FormatVersion <= 0)
        {
            document.FormatVersion = NoteDocument.CurrentFormatVersion;
        }

        if (string.IsNullOrWhiteSpace(document.Title))
        {
            document.Title = NoteDocument.DefaultTitle;
        }
        else if (document.Title.Length > MaxTitleLength)
        {
            document.Title = document.Title[..MaxTitleLength];
        }

        var defaultDims = PageDimensions.Clamp(document.DefaultPageWidth, document.DefaultPageHeight);
        document.DefaultPageWidth = defaultDims.Width;
        document.DefaultPageHeight = defaultDims.Height;

        document.Pages.RemoveAll(static p => p is null);

        for (var i = 0; i < document.Pages.Count; i++)
        {
            SanitizePage(document.Pages[i], i + 1, defaultDims);
        }

        return document;
    }

    public static void SanitizePage(NotePage page, int fallbackPageNumber, PageDimensions fallbackDimensions)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.PageNumber <= 0)
        {
            page.PageNumber = Math.Max(1, fallbackPageNumber);
        }

        var pageDims = PageDimensions.Clamp(page.Width, page.Height, fallbackDimensions);
        page.Width = pageDims.Width;
        page.Height = pageDims.Height;

        if (!IsValidHexColor(page.BackgroundHex))
        {
            page.BackgroundHex = NotePage.DefaultBackgroundHex;
        }

        if (page.IsfEntryPath is not null && !IsValidIsfEntryPath(page.IsfEntryPath))
        {
            page.IsfEntryPath = null;
        }

        page.Elements.RemoveAll(static el =>
            el is null || (el is ImageCanvasElement img && !IsValidAssetKey(img.AssetKey)));

        foreach (var element in page.Elements)
        {
            SanitizeElement(element);
        }
    }

    public static void SanitizeElement(CanvasElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.X = ClampFinite(element.X, MinElementCoordinateDip, MaxElementCoordinateDip, 0.0);
        element.Y = ClampFinite(element.Y, MinElementCoordinateDip, MaxElementCoordinateDip, 0.0);
        element.Width = ClampFinite(element.Width, MinElementDimensionDip, MaxElementDimensionDip, 200.0);
        element.Height = ClampFinite(element.Height, MinElementDimensionDip, MaxElementDimensionDip, 100.0);

        switch (element)
        {
            case TextCanvasElement textElement:
                textElement.Text ??= string.Empty;
                if (textElement.Text.Length > MaxTextLength)
                {
                    textElement.Text = textElement.Text[..MaxTextLength];
                }

                if (string.IsNullOrWhiteSpace(textElement.FontFamily) ||
                    textElement.FontFamily.Length > MaxFontFamilyLength)
                {
                    textElement.FontFamily = TextCanvasElement.DefaultFontFamily;
                }

                textElement.FontSize = ClampFinite(
                    textElement.FontSize, MinFontSize, MaxFontSize, TextCanvasElement.DefaultFontSize);

                if (!IsValidHexColor(textElement.ForegroundHex))
                {
                    textElement.ForegroundHex = TextCanvasElement.DefaultForegroundHex;
                }
                break;

            case ImageCanvasElement imageElement:
                imageElement.CropX = ClampFinite(imageElement.CropX, 0.0, MaxElementDimensionDip, 0.0);
                imageElement.CropY = ClampFinite(imageElement.CropY, 0.0, MaxElementDimensionDip, 0.0);
                imageElement.CropWidth = ClampFinite(imageElement.CropWidth, 0.0, MaxElementDimensionDip, 0.0);
                imageElement.CropHeight = ClampFinite(imageElement.CropHeight, 0.0, MaxElementDimensionDip, 0.0);
                break;
        }
    }

    private static double ClampFinite(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}

