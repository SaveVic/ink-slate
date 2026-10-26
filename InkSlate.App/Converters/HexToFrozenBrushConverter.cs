using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.App.Converters;

public sealed class HexToFrozenBrushConverter : IValueConverter
{
    private const int MaxCacheSize = 64;
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object SyncLock = new();

    private static readonly SolidColorBrush FallbackBrush;

    static HexToFrozenBrushConverter()
    {
        FallbackBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        FallbackBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return FallbackBrush;
        }

        return GetOrCreateFrozenBrush(hex.Trim());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public static SolidColorBrush GetOrCreateFrozenBrush(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        lock (SyncLock)
        {
            if (Cache.TryGetValue(hex, out var cached))
            {
                return cached;
            }

            if (!TryParseHexColor(hex, out var color))
            {
                return FallbackBrush;
            }

            if (Cache.Count >= MaxCacheSize)
            {
                Cache.Clear();
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            Cache[hex] = brush;
            return brush;
        }
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        color = default;
        if (!hex.StartsWith('#'))
        {
            return false;
        }

        var span = hex.AsSpan(1);
        if (span.Length == 6)
        {
            if (byte.TryParse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(span.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(span.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                color = Color.FromRgb(r, g, b);
                return true;
            }
        }
        else if (span.Length == 8)
        {
            if (byte.TryParse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a) &&
                byte.TryParse(span.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(span.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(span.Slice(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                color = Color.FromArgb(a, r, g, b);
                return true;
            }
        }

        return false;
    }
}

