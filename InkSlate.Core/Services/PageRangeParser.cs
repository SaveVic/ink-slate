using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using InkSlate.Core.Models;

namespace InkSlate.Core.Services;

/// <summary>
/// Provides pure parsing and validation for page range strings (e.g. "1, 3, 5-8")
/// and maps export modes to 0-based sorted, deduplicated page indices.
/// </summary>
public static class PageRangeParser
{
    /// <summary>
    /// Attempts to parse a 1-based user page range string into a sorted, deduplicated list of 0-based page indices.
    /// Returns false if the input contains invalid syntax, inverted ranges, non-numbers, or out-of-bounds page numbers.
    /// </summary>
    public static bool TryParsePageRange(
        string? input,
        int totalPages,
        [NotNullWhen(true)] out IReadOnlyList<int>? pageIndices)
    {
        pageIndices = null;

        if (totalPages <= 0 || string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var tokens = input.Split(',');
        var uniqueIndices = new HashSet<int>();

        foreach (var rawToken in tokens)
        {
            var token = rawToken.Trim();
            if (token.Length == 0)
            {
                return false;
            }

            var dashCount = token.Count(static c => c == '-');
            if (dashCount == 0)
            {
                if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var pageNum) ||
                    pageNum <= 0 ||
                    pageNum > totalPages)
                {
                    return false;
                }

                uniqueIndices.Add(pageNum - 1);
            }
            else if (dashCount == 1)
            {
                var dashIndex = token.IndexOf('-');
                var startPart = token[..dashIndex].Trim();
                var endPart = token[(dashIndex + 1)..].Trim();

                if (startPart.Length == 0 ||
                    endPart.Length == 0 ||
                    !int.TryParse(startPart, NumberStyles.None, CultureInfo.InvariantCulture, out var startNum) ||
                    !int.TryParse(endPart, NumberStyles.None, CultureInfo.InvariantCulture, out var endNum) ||
                    startNum <= 0 ||
                    endNum <= 0 ||
                    startNum > endNum ||
                    endNum > totalPages)
                {
                    return false;
                }

                for (var p = startNum; p <= endNum; p++)
                {
                    uniqueIndices.Add(p - 1);
                }
            }
            else
            {
                // Invalid: multiple dashes (e.g., "1-2-3", "--5")
                return false;
            }
        }

        if (uniqueIndices.Count == 0)
        {
            return false;
        }

        pageIndices = uniqueIndices.Order().ToArray();
        return true;
    }

    /// <summary>
    /// Parses a 1-based user page range string into 0-based page indices, throwing <see cref="FormatException"/> on failure.
    /// </summary>
    public static IReadOnlyList<int> ParsePageRange(string input, int totalPages)
    {
        if (!TryParsePageRange(input, totalPages, out var pageIndices))
        {
            throw new FormatException(
                $"Invalid page range '{input}' for document with {totalPages} page(s). Expected format like '1, 3, 5-8'.");
        }

        return pageIndices;
    }

    /// <summary>
    /// Resolves target page indices based on the selected <see cref="PdfExportPageMode"/>.
    /// </summary>
    public static bool TryResolvePageIndices(
        PdfExportPageMode mode,
        int totalPages,
        int activePageIndex,
        string? customRange,
        [NotNullWhen(true)] out IReadOnlyList<int>? pageIndices)
    {
        pageIndices = null;

        if (totalPages <= 0)
        {
            return false;
        }

        switch (mode)
        {
            case PdfExportPageMode.AllPages:
                pageIndices = Enumerable.Range(0, totalPages).ToArray();
                return true;

            case PdfExportPageMode.CurrentPage:
                if (activePageIndex < 0 || activePageIndex >= totalPages)
                {
                    return false;
                }
                pageIndices = [activePageIndex];
                return true;

            case PdfExportPageMode.CustomRange:
                return TryParsePageRange(customRange, totalPages, out pageIndices);

            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves target page indices based on the selected <see cref="PdfExportPageMode"/>, throwing if invalid.
    /// </summary>
    public static IReadOnlyList<int> ResolvePageIndices(
        PdfExportPageMode mode,
        int totalPages,
        int activePageIndex,
        string? customRange = null)
    {
        if (!TryResolvePageIndices(mode, totalPages, activePageIndex, customRange, out var pageIndices))
        {
            throw new FormatException(
                $"Failed to resolve page indices for export mode '{mode}' (totalPages: {totalPages}, activePageIndex: {activePageIndex}, customRange: '{customRange}').");
        }

        return pageIndices;
    }
}
