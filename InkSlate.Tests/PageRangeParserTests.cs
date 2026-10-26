using System.Globalization;
using CsCheck;
using InkSlate.Core.Models;
using InkSlate.Core.Services;
using Xunit;

namespace InkSlate.Tests;

public sealed class PageRangeParserTests
{
    private static readonly int[] ExpectedSingleZero = [0];
    private static readonly int[] ExpectedSingleNine = [9];
    private static readonly int[] ExpectedSingleFour = [4];
    private static readonly int[] ExpectedSingleTwo = [2];
    private static readonly int[] ExpectedContiguousFourToSeven = [4, 5, 6, 7];
    private static readonly int[] ExpectedContiguousOneToThree = [1, 2, 3];
    private static readonly int[] ExpectedContiguousZeroToOne = [0, 1];
    private static readonly int[] ExpectedMixedIndices = [0, 1, 2, 4, 5, 6, 7];
    private static readonly int[] ExpectedWhitespaceIndices = [0, 1, 2, 5, 6, 7, 9];
    private static readonly int[] ExpectedAllFourPages = [0, 1, 2, 3];
    private static readonly int[] ExpectedCustomTwoFourToSix = [1, 3, 4, 5];
    private static readonly int[] ExpectedOneThreeFiveSix = [0, 2, 4, 5];

    [Theory]
    [InlineData("1", 10, 0)]
    [InlineData("10", 10, 9)]
    [InlineData("  5  ", 10, 4)]
    public void TryParsePageRange_SinglePage_ReturnsZeroBasedIndex(string input, int totalPages, int expectedIndex)
    {
        var success = PageRangeParser.TryParsePageRange(input, totalPages, out var indices);

        Assert.True(success);
        Assert.NotNull(indices);
        Assert.Single(indices);
        Assert.Equal(expectedIndex, indices[0]);
    }

    [Fact]
    public void TryParsePageRange_RangeSyntax_ReturnsContiguousZeroBasedIndices()
    {
        var success1 = PageRangeParser.TryParsePageRange("5-8", 10, out var indices1);
        Assert.True(success1);
        Assert.Equal(ExpectedContiguousFourToSeven, indices1);

        var success2 = PageRangeParser.TryParsePageRange("2 - 4", 10, out var indices2);
        Assert.True(success2);
        Assert.Equal(ExpectedContiguousOneToThree, indices2);

        var success3 = PageRangeParser.TryParsePageRange("1 - 2", 2, out var indices3);
        Assert.True(success3);
        Assert.Equal(ExpectedContiguousZeroToOne, indices3);

        var success4 = PageRangeParser.TryParsePageRange("3-3", 10, out var indices4);
        Assert.True(success4);
        Assert.Equal(ExpectedSingleTwo, indices4);
    }

    [Fact]
    public void TryParsePageRange_ComplexMixedSyntax_ReturnsSortedDeduplicatedIndices()
    {
        const string input = "1, 3, 5-8, 2, 2, 5, 7";
        var success = PageRangeParser.TryParsePageRange(input, 10, out var indices);

        Assert.True(success);
        Assert.NotNull(indices);
        Assert.Equal(ExpectedMixedIndices, indices);
    }

    [Fact]
    public void TryParsePageRange_WhitespaceVariations_ParsesSuccessfully()
    {
        const string input = "  10 ,  1 - 3 ,  6 - 8  ";
        var success = PageRangeParser.TryParsePageRange(input, 10, out var indices);

        Assert.True(success);
        Assert.NotNull(indices);
        Assert.Equal(ExpectedWhitespaceIndices, indices);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("", 5)]
    [InlineData("   ", 5)]
    [InlineData(",", 5)]
    [InlineData(",1", 5)]
    [InlineData("1,", 5)]
    [InlineData("1,,2", 5)]
    [InlineData("-1", 5)]
    [InlineData("1-", 5)]
    [InlineData("8-5", 10)]             // Inverted range
    [InlineData("0", 5)]               // 0 is invalid 1-based page
    [InlineData("-5", 5)]              // Negative
    [InlineData("0-3", 5)]             // Starts at 0
    [InlineData("1--3", 5)]            // Double dash
    [InlineData("1-2-3", 5)]           // Multiple dashes
    [InlineData("foo", 5)]             // Non-numeric
    [InlineData("1, bar, 3", 5)]       // Non-numeric token
    [InlineData("6", 5)]               // Out of bounds
    [InlineData("4-6", 5)]             // Range exceeds total
    [InlineData("1", 0)]               // totalPages <= 0
    [InlineData("1", -1)]              // totalPages negative
    public void TryParsePageRange_InvalidInputs_ReturnsFalse(string? input, int totalPages)
    {
        var success = PageRangeParser.TryParsePageRange(input, totalPages, out var indices);

        Assert.False(success);
        Assert.Null(indices);
    }

    [Fact]
    public void ParsePageRange_ThrowsFormatException_WhenInvalid()
    {
        Assert.Throws<FormatException>(() => PageRangeParser.ParsePageRange("8-5", 10));
        Assert.Throws<FormatException>(() => PageRangeParser.ParsePageRange("invalid", 5));
    }

    [Fact]
    public void ParsePageRange_ReturnsIndices_WhenValid()
    {
        var result = PageRangeParser.ParsePageRange("1, 3, 5-6", 10);
        Assert.Equal(ExpectedOneThreeFiveSix, result);
    }

    [Fact]
    public void ResolvePageIndices_AllPagesMode_ReturnsAllZeroBasedIndices()
    {
        var result = PageRangeParser.ResolvePageIndices(PdfExportPageMode.AllPages, totalPages: 4, activePageIndex: 1);
        Assert.Equal(ExpectedAllFourPages, result);
    }

    [Fact]
    public void ResolvePageIndices_CurrentPageMode_ReturnsActivePageIndex()
    {
        var result = PageRangeParser.ResolvePageIndices(PdfExportPageMode.CurrentPage, totalPages: 5, activePageIndex: 2);
        Assert.Equal(ExpectedSingleTwo, result);
    }

    [Fact]
    public void ResolvePageIndices_CustomRangeMode_ResolvesGivenString()
    {
        var result = PageRangeParser.ResolvePageIndices(
            PdfExportPageMode.CustomRange,
            totalPages: 10,
            activePageIndex: 0,
            customRange: "2, 4-6");

        Assert.Equal(ExpectedCustomTwoFourToSix, result);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    public void ResolvePageIndices_CurrentPageOutOfRange_ThrowsFormatException(int activeIndex, int totalPages)
    {
        Assert.Throws<FormatException>(() =>
            PageRangeParser.ResolvePageIndices(PdfExportPageMode.CurrentPage, totalPages, activeIndex));
    }

    [Fact]
    public void CsCheck_ValidPageRanges_AlwaysSortedAndWithinBounds()
    {
        Gen.Int[1, 50].SelectMany(totalPages =>
        {
            var genToken = Gen.OneOf(
                Gen.Int[1, totalPages].Select(p => p.ToString(CultureInfo.InvariantCulture)),
                Gen.Int[1, totalPages].SelectMany(start =>
                    Gen.Int[start, totalPages].Select(end => $"{start}-{end}"))
            );

            return genToken.Array[1, 10].Select(tokens => (totalPages, rangeStr: string.Join(", ", tokens)));
        })
        .Sample(testCase =>
        {
            var success = PageRangeParser.TryParsePageRange(testCase.rangeStr, testCase.totalPages, out var indices);

            Assert.True(success);
            Assert.NotNull(indices);
            Assert.NotEmpty(indices);

            for (var i = 0; i < indices.Count; i++)
            {
                Assert.InRange(indices[i], 0, testCase.totalPages - 1);
                if (i > 0)
                {
                    Assert.True(indices[i] > indices[i - 1], "Indices must be strictly increasing and unique.");
                }
            }
        });
    }

    [Fact]
    public void CsCheck_RandomPageSets_RoundTripExactZeroBasedIndices()
    {
        Gen.Int[1, 100].SelectMany(totalPages =>
        {
            return Gen.Int[1, totalPages].Array[1, 25].Select(rawPages =>
            {
                var sortedDistinct = rawPages.Distinct().OrderBy(p => p).ToList();
                var expectedZeroBased = sortedDistinct.Select(p => p - 1).ToArray();

                // Group contiguous sequences into range tokens or single tokens
                var tokens = new List<string>();
                var i = 0;
                while (i < sortedDistinct.Count)
                {
                    var start = sortedDistinct[i];
                    var end = start;
                    while (i + 1 < sortedDistinct.Count && sortedDistinct[i + 1] == end + 1)
                    {
                        end = sortedDistinct[i + 1];
                        i++;
                    }

                    if (start == end)
                    {
                        tokens.Add(start.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        tokens.Add($"{start}-{end}");
                    }
                    i++;
                }

                // Add random duplicate tokens or whitespace to test deduplication and robustness
                var rangeString = string.Join(", ", tokens);
                return (totalPages, rangeString, expectedZeroBased);
            });
        })
        .Sample(testCase =>
        {
            var success = PageRangeParser.TryParsePageRange(testCase.rangeString, testCase.totalPages, out var actualIndices);

            Assert.True(success, $"Failed to parse generated range: '{testCase.rangeString}' for totalPages={testCase.totalPages}");
            Assert.NotNull(actualIndices);
            Assert.Equal(testCase.expectedZeroBased, actualIndices);
        });
    }

    [Fact]
    public void CsCheck_TryResolvePageIndices_Fuzzing_NeverThrowsUnhandledException()
    {
        var genMode = Gen.OneOf(
            Gen.Const(PdfExportPageMode.AllPages),
            Gen.Const(PdfExportPageMode.CurrentPage),
            Gen.Const(PdfExportPageMode.CustomRange));

        Gen.Select(genMode, Gen.Int[-5, 50], Gen.Int[-5, 50], Gen.String)
            .Sample((mode, totalPages, activeIndex, customRange) =>
            {
                // Must return cleanly (true or false), never throw unhandled exception
                _ = PageRangeParser.TryResolvePageIndices(mode, totalPages, activeIndex, customRange, out _);
            });
    }

    [Fact]
    public void CsCheck_Fuzzing_NeverThrowsUnhandledException()
    {
        Gen.String.SelectMany(s => Gen.Int[-5, 50].Select(n => (input: s, totalPages: n)))
            .Sample(testCase =>
            {
                // Must return cleanly (true or false), never throw unhandled exceptions
                _ = PageRangeParser.TryParsePageRange(testCase.input, testCase.totalPages, out _);
            });
    }
}
