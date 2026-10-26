using System.Globalization;
using System.Windows.Media;
using InkSlate.App.Converters;
using Xunit;

namespace InkSlate.Tests;

public sealed class HexToFrozenBrushConverterTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void ValidRgbHexProducesFrozenBrush()
    {
        RunInSta(() =>
        {
            var converter = new HexToFrozenBrushConverter();
            var result = converter.Convert("#FF2563EB", typeof(Brush), null, CultureInfo.InvariantCulture);

            var brush = Assert.IsType<SolidColorBrush>(result);
            Assert.True(brush.IsFrozen);
            Assert.Equal(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB), brush.Color);
        });
    }

    [Fact]
    public void SixDigitHexDefaultsToFullAlpha()
    {
        RunInSta(() =>
        {
            var converter = new HexToFrozenBrushConverter();
            var result = converter.Convert("#1E1E1E", typeof(Brush), null, CultureInfo.InvariantCulture);

            var brush = Assert.IsType<SolidColorBrush>(result);
            Assert.True(brush.IsFrozen);
            Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), brush.Color);
        });
    }

    [Fact]
    public void InvalidHexFallsBackToDefaultFrozenBrush()
    {
        RunInSta(() =>
        {
            var converter = new HexToFrozenBrushConverter();

            var nullResult = Assert.IsType<SolidColorBrush>(converter.Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture));
            var emptyResult = Assert.IsType<SolidColorBrush>(converter.Convert("", typeof(Brush), null, CultureInfo.InvariantCulture));
            var badResult = Assert.IsType<SolidColorBrush>(converter.Convert("not-a-color", typeof(Brush), null, CultureInfo.InvariantCulture));

            Assert.True(nullResult.IsFrozen);
            Assert.True(emptyResult.IsFrozen);
            Assert.True(badResult.IsFrozen);

            Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), nullResult.Color);
            Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), emptyResult.Color);
            Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), badResult.Color);
        });
    }

    [Fact]
    public void CacheReusesIdenticalInstancesAndIsBounded()
    {
        RunInSta(() =>
        {
            var brush1 = HexToFrozenBrushConverter.GetOrCreateFrozenBrush("#FF123456");
            var brush2 = HexToFrozenBrushConverter.GetOrCreateFrozenBrush("#FF123456");
            Assert.Same(brush1, brush2);

            // Adding > 64 unique colors clears and stays bounded
            for (var i = 0; i < 80; i++)
            {
                var hex = $"#{i:X2}{i:X2}{i:X2}";
                var b = HexToFrozenBrushConverter.GetOrCreateFrozenBrush(hex);
                Assert.True(b.IsFrozen);
            }
        });
    }

    [Fact]
    public void ConvertBackThrowsNotSupportedException()
    {
        var converter = new HexToFrozenBrushConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));
    }
}

