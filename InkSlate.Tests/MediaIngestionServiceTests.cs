using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CsCheck;
using InkSlate.Core.Services;
using Xunit;

namespace InkSlate.Tests;

public sealed class MediaIngestionServiceTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void PngAndJpegAndBmpMagicBytesAreDetectedCorrectly()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
        byte[] bmp = [0x42, 0x4D, 0x36, 0x00, 0x00, 0x00, 0x00, 0x00];

        Assert.True(MediaIngestionService.TryDetectImageFormat(png, out var pngFormat));
        Assert.Equal(ImageFormat.Png, pngFormat);

        Assert.True(MediaIngestionService.TryDetectImageFormat(jpeg, out var jpegFormat));
        Assert.Equal(ImageFormat.Jpeg, jpegFormat);

        Assert.True(MediaIngestionService.TryDetectImageFormat(bmp, out var bmpFormat));
        Assert.Equal(ImageFormat.Bmp, bmpFormat);
    }

    [Fact]
    public void ArbitraryExecutablesAndHtmlAreRejected()
    {
        byte[] mzExe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        Assert.Throws<InvalidDataException>(() => MediaIngestionService.IngestBytes(mzExe));

        byte[] html = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body><h1>test</h1></body></html>");
        Assert.Throws<InvalidDataException>(() => MediaIngestionService.IngestBytes(html));

        byte[] shortBytes = [0x89, 0x50, 0x4E];
        Assert.Throws<InvalidDataException>(() => MediaIngestionService.IngestBytes(shortBytes));
    }

    [Fact]
    public void OversizedPayloadExceeding25MbIsRejected()
    {
        var oversizedStream = new MemoryStream(new byte[MediaIngestionService.MaxImageFileSize + 1024]);
        Assert.Throws<InvalidDataException>(() => MediaIngestionService.IngestStream(oversizedStream));
    }

    [Fact]
    public void ValidPngImageIngestionProducesFrozenBitmapAndValidResult()
    {
        RunInSta(() =>
        {
            var pngBytes = CreateTestImageBytes(80, 60, ImageFormat.Png);
            var result = MediaIngestionService.IngestBytes(pngBytes);

            Assert.NotNull(result);
            Assert.StartsWith("assets/", result.AssetKey);
            Assert.EndsWith(".png", result.AssetKey);
            Assert.NotNull(result.FrozenBitmap);
            Assert.True(result.FrozenBitmap.IsFrozen);
            Assert.Equal(80, result.PixelWidth);
            Assert.Equal(60, result.PixelHeight);
        });
    }

    [Fact]
    public void BmpIsTranscodedToCompressedPng()
    {
        RunInSta(() =>
        {
            var bmpBytes = CreateTestImageBytes(40, 40, ImageFormat.Bmp);
            var result = MediaIngestionService.IngestBytes(bmpBytes);

            Assert.NotNull(result);
            Assert.EndsWith(".png", result.AssetKey);
            Assert.NotNull(result.SanitizedBytes);

            // Sanitized bytes must now start with PNG header
            byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            Assert.True(result.SanitizedBytes.AsSpan(0, 8).SequenceEqual(pngHeader));
        });
    }

    [Fact]
    public void CsCheckPropertyFuzzing_RandomByteStreamsNeverCrashUnhandled()
    {
        Gen.Byte.Array[0, 256].Sample(bytes =>
        {
            var detected = MediaIngestionService.TryDetectImageFormat(bytes, out _);
            if (!detected)
            {
                Assert.Throws<InvalidDataException>(() => MediaIngestionService.IngestBytes(bytes));
            }
        });
    }

    private static byte[] CreateTestImageBytes(int width, int height, ImageFormat format)
    {
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Blue, null, new System.Windows.Rect(0, 0, width, height));
        }
        rtb.Render(visual);

        BitmapEncoder encoder = format switch
        {
            ImageFormat.Bmp => new BmpBitmapEncoder(),
            ImageFormat.Jpeg => new JpegBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };

        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}

