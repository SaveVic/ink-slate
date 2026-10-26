using CsCheck;
using InkSlate.Core.Models;
using Xunit;

namespace InkSlate.Tests;

public sealed class PageDimensionPresetTests
{
    [Fact]
    public void PresetsHaveExpectedDimensions()
    {
        Assert.Equal(793.7, PageDimensions.A4.Width);
        Assert.Equal(1122.5, PageDimensions.A4.Height);

        Assert.Equal(816.0, PageDimensions.Letter.Width);
        Assert.Equal(1056.0, PageDimensions.Letter.Height);

        Assert.Equal(816.0, PageDimensions.Legal.Width);
        Assert.Equal(1344.0, PageDimensions.Legal.Height);

        Assert.Equal(1280.0, PageDimensions.Slide16x9.Width);
        Assert.Equal(720.0, PageDimensions.Slide16x9.Height);
    }

    [Theory]
    [InlineData(PageDimensionPreset.A4, 793.7, 1122.5)]
    [InlineData(PageDimensionPreset.Letter, 816.0, 1056.0)]
    [InlineData(PageDimensionPreset.Legal, 816.0, 1344.0)]
    [InlineData(PageDimensionPreset.Slide16x9, 1280.0, 720.0)]
    [InlineData(PageDimensionPreset.Custom, 793.7, 1122.5)]
    public void FromPresetReturnsCorrectDimensions(PageDimensionPreset preset, double expectedWidth, double expectedHeight)
    {
        var dims = PageDimensions.FromPreset(preset);
        Assert.Equal(expectedWidth, dims.Width);
        Assert.Equal(expectedHeight, dims.Height);
    }

    [Fact]
    public void MillimetersConversionRoundTripsAccurately()
    {
        Gen.Double[1.0, 2000.0].Sample(mm =>
        {
            var dip = PageDimensions.MillimetersToDip(mm);
            var roundTripMm = PageDimensions.DipToMillimeters(dip);
            return Math.Abs(mm - roundTripMm) < 1e-9;
        });
    }

    [Fact]
    public void CustomRejectsInvalidDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PageDimensions.Custom(99.0, 500.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageDimensions.Custom(500.0, 99.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageDimensions.Custom(10001.0, 500.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageDimensions.Custom(500.0, 10001.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageDimensions.Custom(double.NaN, 500.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageDimensions.Custom(500.0, double.PositiveInfinity));
    }

    [Fact]
    public void TryCreateCustomValidatesCorrectly()
    {
        Assert.True(PageDimensions.TryCreateCustom(500.0, 700.0, out var valid));
        Assert.Equal(500.0, valid.Width);
        Assert.Equal(700.0, valid.Height);

        Assert.False(PageDimensions.TryCreateCustom(50.0, 700.0, out _));
        Assert.False(PageDimensions.TryCreateCustom(500.0, 50.0, out _));
        Assert.False(PageDimensions.TryCreateCustom(double.NaN, 700.0, out _));
    }

    [Fact]
    public void ClampAlwaysReturnsSafeDimensionsUnderFuzzing()
    {
        Gen.Select(Gen.Double, Gen.Double).Sample((w, h) =>
        {
            var clamped = PageDimensions.Clamp(w, h);
            return double.IsFinite(clamped.Width)
                && double.IsFinite(clamped.Height)
                && clamped.Width >= PageDimensions.MinDimensionDip
                && clamped.Width <= PageDimensions.MaxDimensionDip
                && clamped.Height >= PageDimensions.MinDimensionDip
                && clamped.Height <= PageDimensions.MaxDimensionDip;
        });
    }
}

