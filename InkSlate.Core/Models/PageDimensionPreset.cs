namespace InkSlate.Core.Models;

public enum PageDimensionPreset
{
    A4 = 0,
    Letter = 1,
    Legal = 2,
    Slide16x9 = 3,
    Custom = 4
}

/// <summary>
/// Represents page dimensions in 96-DPI Device-Independent Pixels (DIPs).
/// </summary>
public readonly record struct PageDimensions(double Width, double Height)
{
    public const double WpfDpi = 96.0;
    public const double PdfDpi = 72.0;
    public const double MillimetersPerInch = 25.4;

    public const double MinDimensionDip = 100.0;
    public const double MaxDimensionDip = 10000.0;

    /// <summary>ISO A4 (210mm × 297mm) at 96 DPI: 793.7 × 1122.5 DIPs.</summary>
    public static PageDimensions A4 => new(793.7, 1122.5);

    /// <summary>US Letter (8.5in × 11in) at 96 DPI: 816.0 × 1056.0 DIPs.</summary>
    public static PageDimensions Letter => new(816.0, 1056.0);

    /// <summary>US Legal (8.5in × 14in) at 96 DPI: 816.0 × 1344.0 DIPs.</summary>
    public static PageDimensions Legal => new(816.0, 1344.0);

    /// <summary>16:9 Presentation Slide at 96 DPI: 1280.0 × 720.0 DIPs.</summary>
    public static PageDimensions Slide16x9 => new(1280.0, 720.0);

    public static double MillimetersToDip(double millimeters) =>
        millimeters * WpfDpi / MillimetersPerInch;

    public static double DipToMillimeters(double dip) =>
        dip * MillimetersPerInch / WpfDpi;

    public static PageDimensions FromPreset(PageDimensionPreset preset) => preset switch
    {
        PageDimensionPreset.A4 => A4,
        PageDimensionPreset.Letter => Letter,
        PageDimensionPreset.Legal => Legal,
        PageDimensionPreset.Slide16x9 => Slide16x9,
        _ => A4
    };

    public static PageDimensions Custom(double widthDip, double heightDip)
    {
        if (!IsValidDimension(widthDip))
        {
            throw new ArgumentOutOfRangeException(nameof(widthDip), widthDip,
                $"Page width must be a finite number between {MinDimensionDip} and {MaxDimensionDip} DIPs.");
        }

        if (!IsValidDimension(heightDip))
        {
            throw new ArgumentOutOfRangeException(nameof(heightDip), heightDip,
                $"Page height must be a finite number between {MinDimensionDip} and {MaxDimensionDip} DIPs.");
        }

        return new PageDimensions(widthDip, heightDip);
    }

    public static bool TryCreateCustom(double widthDip, double heightDip, out PageDimensions dimensions)
    {
        if (IsValidDimension(widthDip) && IsValidDimension(heightDip))
        {
            dimensions = new PageDimensions(widthDip, heightDip);
            return true;
        }

        dimensions = default;
        return false;
    }

    public static PageDimensions CustomFromMillimeters(double widthMm, double heightMm) =>
        Custom(MillimetersToDip(widthMm), MillimetersToDip(heightMm));

    public static PageDimensions Clamp(double widthDip, double heightDip, PageDimensions fallback = default)
    {
        var defaultDims = fallback.Width >= MinDimensionDip && fallback.Height >= MinDimensionDip
            ? fallback
            : A4;

        var safeWidth = double.IsFinite(widthDip)
            ? Math.Clamp(widthDip, MinDimensionDip, MaxDimensionDip)
            : defaultDims.Width;

        var safeHeight = double.IsFinite(heightDip)
            ? Math.Clamp(heightDip, MinDimensionDip, MaxDimensionDip)
            : defaultDims.Height;

        return new PageDimensions(safeWidth, safeHeight);
    }

    public static bool IsValidDimension(double value) =>
        double.IsFinite(value) && value >= MinDimensionDip && value <= MaxDimensionDip;
}

