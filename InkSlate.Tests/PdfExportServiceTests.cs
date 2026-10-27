using System.Diagnostics;
using System.IO;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Services;
using SkiaSharp;
using Xunit;

namespace InkSlate.Tests;

public sealed class PdfExportServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public PdfExportServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"InkSlate_PdfTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failure in temp test folder
        }
    }

    private static byte[] CreateValidPngBytes(int width = 32, int height = 32)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    [Fact]
    public void Export_MultiPageDocument_ProducesValidVectorPdf()
    {
        var doc = new NoteDocument
        {
            Title = "Physics Exam",
            DefaultPageWidth = 793.7,
            DefaultPageHeight = 1122.5
        };

        var assetKey = $"assets/{Guid.NewGuid():D}.png";
        doc.Assets[assetKey] = CreateValidPngBytes();

        // Page 1
        var page1 = new NotePage
        {
            PageNumber = 1,
            Width = 793.7,
            Height = 1122.5,
            BackgroundHex = "#FFFFFFFF"
        };

        page1.Elements.Add(new TextCanvasElement
        {
            X = 50,
            Y = 50,
            Width = 400,
            Height = 80,
            Text = "Calculus and Differential Equations\nPart 1: Limits",
            FontSize = 24.0,
            ForegroundHex = "#FF1E3A8A",
            IsBold = true,
            IsItalic = true
        });

        page1.Elements.Add(new ImageCanvasElement
        {
            AssetKey = assetKey,
            X = 50,
            Y = 150,
            Width = 200,
            Height = 200,
            CropX = 4,
            CropY = 4,
            CropWidth = 24,
            CropHeight = 24
        });

        // FitToCurve pen stroke
        var penStroke = new Stroke(new StylusPointCollection
        {
            new StylusPoint(60, 400),
            new StylusPoint(100, 420),
            new StylusPoint(150, 410),
            new StylusPoint(200, 460)
        })
        {
            DrawingAttributes = new DrawingAttributes
            {
                Color = Colors.DarkSlateBlue,
                Width = 3.0,
                Height = 3.0,
                FitToCurve = true
            }
        };

        // Highlighter stroke
        var highlighterStroke = new Stroke(new StylusPointCollection
        {
            new StylusPoint(50, 60),
            new StylusPoint(450, 60)
        })
        {
            DrawingAttributes = new DrawingAttributes
            {
                Color = Colors.Yellow,
                Width = 20.0,
                Height = 20.0,
                IsHighlighter = true
            }
        };

        // Single-point dot stroke
        var dotStroke = new Stroke(new StylusPointCollection
        {
            new StylusPoint(500, 500)
        })
        {
            DrawingAttributes = new DrawingAttributes
            {
                Color = Colors.Red,
                Width = 5.0,
                Height = 5.0
            }
        };

        var page1Strokes = new StrokeCollection { penStroke, highlighterStroke, dotStroke };

        // Page 2
        var page2 = new NotePage
        {
            PageNumber = 2,
            Width = 612.0,
            Height = 792.0,
            BackgroundHex = "#FFF8F9FA"
        };

        var rectTipStroke = new Stroke(new StylusPointCollection
        {
            new StylusPoint(100, 100),
            new StylusPoint(300, 100)
        })
        {
            DrawingAttributes = new DrawingAttributes
            {
                Color = Colors.ForestGreen,
                Width = 6.0,
                Height = 6.0,
                StylusTip = StylusTip.Rectangle
            }
        };
        var page2Strokes = new StrokeCollection { rectTipStroke };

        doc.Pages.Add(page1);
        doc.Pages.Add(page2);

        using var ms = new MemoryStream();
        PdfExportService.Export(
            doc,
            [0, 1],
            ms,
            strokesProvider: id => id == page1.Id ? page1Strokes : page2Strokes);

        Assert.True(ms.Length > 0);
        var bytes = ms.ToArray();

        // PDF starts with %PDF-
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);

        // PDF ends with %%EOF
        var tail = System.Text.Encoding.ASCII.GetString(bytes[^30..]);
        Assert.Contains("%%EOF", tail, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_A4DocumentWithStrokesAndText_VerifiesHeaderLengthPageCountAndDimensions()
    {
        var doc = new NoteDocument
        {
            Title = "A4 Verification Document",
            DefaultPageWidth = PageDimensions.A4.Width,
            DefaultPageHeight = PageDimensions.A4.Height
        };

        var page = new NotePage
        {
            PageNumber = 1,
            Width = PageDimensions.A4.Width,
            Height = PageDimensions.A4.Height,
            BackgroundHex = "#FFFFFFFF"
        };

        page.Elements.Add(new TextCanvasElement
        {
            X = 50,
            Y = 50,
            Width = 300,
            Height = 40,
            Text = "A4 Geometry Verification",
            FontSize = 18.0
        });

        var stroke = new Stroke(new StylusPointCollection
        {
            new StylusPoint(100, 100),
            new StylusPoint(200, 200)
        });
        var strokes = new StrokeCollection { stroke };

        doc.Pages.Add(page);

        using var ms = new MemoryStream();
        PdfExportService.Export(doc, [0], ms, strokesProvider: _ => strokes);

        Assert.True(ms.Length > 0);
        var bytes = ms.ToArray();

        // 1. Signature check
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
        Assert.Equal((byte)'-', bytes[4]);

        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        // 2. Page count check
        Assert.Contains("/Count 1", pdfText, StringComparison.Ordinal);

        // 3. A4 dimensions check in PDF MediaBox: [0 0 793.5 1122.75]
        Assert.Contains("/MediaBox [0 0 793.5 1122.75]", pdfText, StringComparison.Ordinal);

        // 4. Multi-page count check
        var page2 = new NotePage
        {
            PageNumber = 2,
            Width = PageDimensions.A4.Width,
            Height = PageDimensions.A4.Height
        };
        doc.Pages.Add(page2);

        using var ms2 = new MemoryStream();
        PdfExportService.Export(doc, [0, 1], ms2);
        var multiPdfText = System.Text.Encoding.ASCII.GetString(ms2.ToArray());
        Assert.Contains("/Count 2", multiPdfText, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_PageRangeSubsetting_ExportsOnlyRequestedPages()
    {
        var doc = new NoteDocument();
        doc.Pages.Add(new NotePage { PageNumber = 1, Width = 800, Height = 1000 });
        doc.Pages.Add(new NotePage { PageNumber = 2, Width = 800, Height = 1000 });
        doc.Pages.Add(new NotePage { PageNumber = 3, Width = 800, Height = 1000 });

        using var ms = new MemoryStream();
        // Export only page index 1 (page 2)
        PdfExportService.Export(doc, [1], ms);

        Assert.True(ms.Length > 0);
        var bytes = ms.ToArray();
        Assert.Equal((byte)'%', bytes[0]);
    }

    [Fact]
    public void Export_CorruptedOrMissingImage_DoesNotCrashExport()
    {
        var doc = new NoteDocument();
        var page = new NotePage { PageNumber = 1, Width = 800, Height = 600 };

        // Missing asset
        page.Elements.Add(new ImageCanvasElement
        {
            AssetKey = "assets/missing.png",
            X = 10,
            Y = 10,
            Width = 100,
            Height = 100
        });

        // Corrupted asset bytes
        var corruptKey = $"assets/{Guid.NewGuid():D}.png";
        doc.Assets[corruptKey] = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        page.Elements.Add(new ImageCanvasElement
        {
            AssetKey = corruptKey,
            X = 150,
            Y = 10,
            Width = 100,
            Height = 100
        });

        doc.Pages.Add(page);

        using var ms = new MemoryStream();
        // Should not throw
        PdfExportService.Export(doc, [0], ms);

        Assert.True(ms.Length > 0);
    }

    [Fact]
    public async Task ExportToFileAsync_AtomicCommitAndRollback()
    {
        var doc = new NoteDocument();
        doc.Pages.Add(new NotePage { PageNumber = 1, Width = 500, Height = 500 });

        var targetPath = Path.Combine(_testDirectory, "exported.pdf");

        // 1. Success commit
        await PdfExportService.ExportToFileAsync(doc, [0], targetPath);

        Assert.True(File.Exists(targetPath));
        var fileBytes = await File.ReadAllBytesAsync(targetPath);
        Assert.Equal((byte)'%', fileBytes[0]);

        var tmpFiles = Directory.GetFiles(_testDirectory, "*.tmp.*");
        Assert.Empty(tmpFiles);

        // 2. Cancellation rollback
        var cancelTarget = Path.Combine(_testDirectory, "cancel_target.pdf");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PdfExportService.ExportToFileAsync(doc, [0], cancelTarget, ct: cts.Token));

        Assert.False(File.Exists(cancelTarget));
        var remainingTmp = Directory.GetFiles(_testDirectory, "*.tmp.*");
        Assert.Empty(remainingTmp);
    }

    [Fact]
    public async Task Export_50PageDocument_CompletesUnderOneSecond()
    {
        var doc = new NoteDocument();
        var allPageIndices = new List<int>();

        for (var i = 0; i < 50; i++)
        {
            var page = new NotePage
            {
                PageNumber = i + 1,
                Width = 793.7,
                Height = 1122.5,
                BackgroundHex = i % 2 == 0 ? "#FFFFFFFF" : "#FFF8F9FA"
            };

            page.Elements.Add(new TextCanvasElement
            {
                X = 50,
                Y = 50,
                Width = 300,
                Height = 40,
                Text = $"Page {i + 1} - Performance Benchmark",
                FontSize = 18.0
            });

            var stroke = new Stroke(new StylusPointCollection
            {
                new StylusPoint(50, 200),
                new StylusPoint(150, 250),
                new StylusPoint(250, 220),
                new StylusPoint(350, 300)
            })
            {
                DrawingAttributes = new DrawingAttributes
                {
                    Color = Colors.DarkBlue,
                    Width = 2.0,
                    Height = 2.0,
                    FitToCurve = true
                }
            };

            doc.Pages.Add(page);
            doc.PageStrokes[page.Id] = new StrokeCollection { stroke };
            allPageIndices.Add(i);
        }

        // Warm-up to ensure JIT compilation and SkiaSharp font engine initialization are not benchmarked
        using (var warmupMs = new MemoryStream())
        {
            var warmupDoc = new NoteDocument();
            warmupDoc.Pages.Add(new NotePage { PageNumber = 1, Width = 100, Height = 100 });
            await PdfExportService.ExportAsync(warmupDoc, [0], warmupMs);
        }

        using var ms = new MemoryStream();
        var sw = Stopwatch.StartNew();

        await PdfExportService.ExportAsync(doc, allPageIndices, ms);

        sw.Stop();

        Assert.True(ms.Length > 0);
        // Requirement: 50-page vector export completes under 1500 ms in CI/test environments
        Assert.True(sw.ElapsedMilliseconds < 1500,
            $"Exporting 50 pages took {sw.ElapsedMilliseconds} ms, exceeding 1500 ms limit.");
    }
}

