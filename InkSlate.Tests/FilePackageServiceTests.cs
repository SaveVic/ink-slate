using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Ink;
using System.Windows.Input;
using CsCheck;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Serialization;
using InkSlate.Core.Services;
using Xunit;

namespace InkSlate.Tests;

public sealed class FilePackageServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public FilePackageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"InkSlate_PkgTests_{Guid.NewGuid():N}");
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
            // Ignore temporary directory cleanup failures
        }
    }

    [Fact]
    public async Task SaveAndLoad_Roundtrip_PreservesAllData()
    {
        var filePath = Path.Combine(_testDirectory, "test_doc.notex");

        var doc = new NoteDocument
        {
            Title = "Physics Notes",
            DefaultPageWidth = 1000.0,
            DefaultPageHeight = 1500.0
        };

        var page1 = new NotePage
        {
            PageNumber = 1,
            Width = 1000.0,
            Height = 1500.0,
            BackgroundHex = "#FFFFFFFF"
        };

        var textElement = new TextCanvasElement
        {
            X = 50,
            Y = 60,
            Width = 300,
            Height = 100,
            Text = "Velocity = Distance / Time",
            FontSize = 24.0,
            ForegroundHex = "#FF112233",
            IsBold = true
        };
        page1.Elements.Add(textElement);

        var assetGuid = Guid.NewGuid();
        var assetKey = $"assets/{assetGuid:D}.png";
        var dummyPngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03 };
        doc.Assets[assetKey] = dummyPngBytes;

        var imgElement = new ImageCanvasElement
        {
            AssetKey = assetKey,
            X = 100,
            Y = 200,
            Width = 400,
            Height = 300,
            CropX = 10,
            CropY = 10,
            CropWidth = 380,
            CropHeight = 280
        };
        page1.Elements.Add(imgElement);

        var page2 = new NotePage
        {
            PageNumber = 2,
            Width = 800.0,
            Height = 1200.0,
            BackgroundHex = "#FFF0F4F8"
        };

        doc.Pages.Add(page1);
        doc.Pages.Add(page2);

        var stroke1 = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(50, 60) });
        var stroke2 = new Stroke(new StylusPointCollection { new StylusPoint(100, 100), new StylusPoint(200, 250) });

        var page1Strokes = new StrokeCollection { stroke1 };
        var page2Strokes = new StrokeCollection { stroke2 };

        // Save
        await FilePackageService.SaveAsync(
            doc,
            filePath,
            strokesProvider: id => id == page1.Id ? page1Strokes : page2Strokes);

        Assert.True(File.Exists(filePath));

        // Load
        var loadedDoc = await FilePackageService.LoadAsync(filePath);

        Assert.NotNull(loadedDoc);
        Assert.Equal("Physics Notes", loadedDoc.Title);
        Assert.Equal(2, loadedDoc.Pages.Count);

        // Verify Page 1
        var loadedP1 = loadedDoc.Pages[0];
        Assert.Equal(page1.Id, loadedP1.Id);
        Assert.Equal(1000.0, loadedP1.Width);
        Assert.Equal(1500.0, loadedP1.Height);
        Assert.Equal("#FFFFFFFF", loadedP1.BackgroundHex);
        Assert.Equal(2, loadedP1.Elements.Count);

        var loadedText = Assert.IsType<TextCanvasElement>(loadedP1.Elements[0]);
        Assert.Equal("Velocity = Distance / Time", loadedText.Text);
        Assert.Equal(24.0, loadedText.FontSize);
        Assert.True(loadedText.IsBold);

        var loadedImg = Assert.IsType<ImageCanvasElement>(loadedP1.Elements[1]);
        Assert.Equal(assetKey, loadedImg.AssetKey);
        Assert.Equal(10.0, loadedImg.CropX);
        Assert.Equal(380.0, loadedImg.CropWidth);

        // Verify Assets
        Assert.True(loadedDoc.Assets.ContainsKey(assetKey));
        Assert.Equal(dummyPngBytes, loadedDoc.Assets[assetKey]);

        // Verify Page 2
        var loadedP2 = loadedDoc.Pages[1];
        Assert.Equal(page2.Id, loadedP2.Id);
        Assert.Equal(800.0, loadedP2.Width);
        Assert.Equal("#FFF0F4F8", loadedP2.BackgroundHex);

        // Verify Strokes
        Assert.True(loadedDoc.PageStrokes.TryGetValue(page1.Id, out var loadedP1Strokes));
        Assert.Single(loadedP1Strokes);
        Assert.Equal(2, loadedP1Strokes[0].StylusPoints.Count);
        Assert.Equal(10.0, loadedP1Strokes[0].StylusPoints[0].X, precision: 1);

        Assert.True(loadedDoc.PageStrokes.TryGetValue(page2.Id, out var loadedP2Strokes));
        Assert.Single(loadedP2Strokes);
        Assert.Equal(200.0, loadedP2Strokes[0].StylusPoints[1].X, precision: 1);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("pages/../../secret.txt")]
    [InlineData("/root/manifest.json")]
    [InlineData("\\windows\\win.ini")]
    [InlineData("C:\\bad.txt")]
    [InlineData("pages\\018f3a2c-0000-0000-0000-000000000001.isf")]
    public async Task LoadAsync_ZipSlipEntry_ThrowsInvalidDataException(string maliciousPath)
    {
        var filePath = Path.Combine(_testDirectory, $"zipslip_{Guid.NewGuid():N}.notex");

        using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            {
                var doc = new NoteDocument();
                JsonSerializer.Serialize(stream, doc, InkSlateJsonContext.Default.NoteDocument);
            }

            var evilEntry = zip.CreateEntry(maliciousPath);
            using (var stream = evilEntry.Open())
            {
                stream.Write("malicious"u8);
            }
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => FilePackageService.LoadAsync(filePath));
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("notes.txt")]
    [InlineData("manifest.xml")]
    [InlineData("assets/not-a-guid.png")]
    [InlineData("pages/not-a-guid.isf")]
    public async Task LoadAsync_DisallowedEntry_ThrowsInvalidDataException(string disallowedEntryName)
    {
        var filePath = Path.Combine(_testDirectory, $"disallowed_{Guid.NewGuid():N}.notex");

        using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            {
                var doc = new NoteDocument();
                JsonSerializer.Serialize(stream, doc, InkSlateJsonContext.Default.NoteDocument);
            }

            var entry = zip.CreateEntry(disallowedEntryName);
            using (var stream = entry.Open())
            {
                stream.Write("data"u8);
            }
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => FilePackageService.LoadAsync(filePath));
    }

    [Fact]
    public void BoundedReadStream_ExceedingQuota_ThrowsInvalidDataException()
    {
        var rawData = new byte[200];
        using var ms = new MemoryStream(rawData);
        using var bounded = new BoundedReadStream(ms, maxBytes: 100);

        var buffer = new byte[60];
        var read1 = bounded.Read(buffer, 0, buffer.Length);
        Assert.Equal(60, read1);

        // Next read of 60 bytes takes total to 120 > 100 maxBytes -> throws
        Assert.Throws<InvalidDataException>(() => bounded.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public async Task BoundedReadStream_ReadAsyncMemory_ThrowsWhenExceedingQuota()
    {
        var rawData = new byte[150];
        using var ms = new MemoryStream(rawData);
        using var bounded = new BoundedReadStream(ms, maxBytes: 80);

        var buffer = new byte[50];
        var read1 = await bounded.ReadAsync(buffer.AsMemory(0, 50));
        Assert.Equal(50, read1);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            _ = await bounded.ReadAsync(buffer.AsMemory(0, 50));
        });
    }

    [Fact]
    public void ArchiveByteTracker_ExceedingCumulativeQuota_ThrowsInvalidDataException()
    {
        var tracker = new ArchiveByteTracker(maxTotalBytes: 250);

        var stream1Data = new byte[150];
        using (var ms1 = new MemoryStream(stream1Data))
        using (var bounded1 = new BoundedReadStream(ms1, maxBytes: 200, tracker))
        {
            var buf = new byte[150];
            var readCount = bounded1.Read(buf, 0, buf.Length);
            Assert.Equal(150, readCount);
            Assert.Equal(150, tracker.TotalBytesRead);
        }

        var stream2Data = new byte[150];
        using (var ms2 = new MemoryStream(stream2Data))
        using (var bounded2 = new BoundedReadStream(ms2, maxBytes: 200, tracker))
        {
            var buf = new byte[150];
            // Cumulative 150 + 150 = 300 > 250 -> throws
            Assert.Throws<InvalidDataException>(() =>
            {
                _ = bounded2.Read(buf, 0, buf.Length);
            });
        }
    }

    [Fact]
    public async Task LoadAsync_MoreThan2000Entries_ThrowsInvalidDataException()
    {
        var filePath = Path.Combine(_testDirectory, $"too_many_entries_{Guid.NewGuid():N}.notex");

        using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            {
                var doc = new NoteDocument();
                JsonSerializer.Serialize(stream, doc, InkSlateJsonContext.Default.NoteDocument);
            }

            for (var i = 0; i < 2005; i++)
            {
                var pageId = Guid.NewGuid();
                zip.CreateEntry($"pages/{pageId:D}.isf");
            }
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => FilePackageService.LoadAsync(filePath));
    }

    [Fact]
    public async Task LoadAsync_CorruptedIsfEntry_SafelyRecoversEmptyStrokes()
    {
        var filePath = Path.Combine(_testDirectory, $"corrupted_isf_{Guid.NewGuid():N}.notex");

        var pageId = Guid.NewGuid();
        var doc = new NoteDocument();
        doc.Pages.Add(new NotePage
        {
            Id = pageId,
            PageNumber = 1,
            IsfEntryPath = $"pages/{pageId:D}.isf"
        });

        using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            {
                JsonSerializer.Serialize(stream, doc, InkSlateJsonContext.Default.NoteDocument);
            }

            var isfEntry = zip.CreateEntry($"pages/{pageId:D}.isf");
            using (var stream = isfEntry.Open())
            {
                // Write random invalid bytes instead of valid ISF data
                stream.Write(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 });
            }
        }

        // Loading should NOT crash
        var loadedDoc = await FilePackageService.LoadAsync(filePath);
        Assert.NotNull(loadedDoc);
        Assert.Single(loadedDoc.Pages);
        Assert.True(loadedDoc.PageStrokes.TryGetValue(pageId, out var strokes));
        Assert.Empty(strokes); // Defensively recovered as empty strokes
    }

    [Fact]
    public async Task LoadAsync_InMemoryZipSlipTraversal_ThrowsInvalidDataExceptionAndTouchesZeroDiskFiles()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            {
                var doc = new NoteDocument();
                JsonSerializer.Serialize(stream, doc, InkSlateJsonContext.Default.NoteDocument);
            }

            var evilEntry = zip.CreateEntry("../../evil.txt");
            using (var stream = evilEntry.Open())
            {
                stream.Write("malicious payload"u8);
            }
        }

        ms.Position = 0;
        var initialFileCount = Directory.GetFiles(_testDirectory).Length;

        await Assert.ThrowsAsync<InvalidDataException>(() => FilePackageService.LoadAsync(ms));

        // Zero files created/touched on disk
        Assert.Equal(initialFileCount, Directory.GetFiles(_testDirectory).Length);
        Assert.False(File.Exists(Path.Combine(_testDirectory, "evil.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_testDirectory)!, "evil.txt")));
    }

    [Fact]
    public async Task LoadAsync_InMemoryDecompressionBombManifest_AbortsAt5MbThreshold()
    {
        // 1. Direct stream test: 20 MB stream exceeds 5 MB threshold
        var rawData = new byte[20 * 1024 * 1024];
        using (var rawMs = new MemoryStream(rawData))
        using (var bounded = new BoundedReadStream(rawMs, maxBytes: FilePackageService.MaxManifestSizeBytes))
        {
            var buffer = new byte[64 * 1024];
            Assert.Throws<InvalidDataException>(() =>
            {
                while (bounded.Read(buffer, 0, buffer.Length) > 0) { }
            });
        }

        // 2. In-memory synthetic ZIP bomb: 20 MB of compressible JSON inside manifest.json
        using var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using var stream = manifestEntry.Open();
            stream.Write("{\"title\":\""u8);
            var repeatedChunk = new byte[64 * 1024];
            Array.Fill(repeatedChunk, (byte)'A');
            for (var i = 0; i < 320; i++) // 320 * 64 KB = 20.48 MB
            {
                stream.Write(repeatedChunk);
            }
            stream.Write("\"}"u8);
        }

        zipMs.Position = 0;
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => FilePackageService.LoadAsync(zipMs));
        Assert.Contains("quota", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_InMemoryTruncatedAndRandomIsfBytes_GracefullyRecoversEmptyStrokes()
    {
        // Part 1: Truncated valid ISF bytes
        var pageId = Guid.NewGuid();
        var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(50, 50) });
        var validStrokes = new StrokeCollection { stroke };

        byte[] validIsfBytes;
        using (var isfMs = new MemoryStream())
        {
            validStrokes.Save(isfMs);
            validIsfBytes = isfMs.ToArray();
        }

        // Truncate to half length
        var truncatedBytes = validIsfBytes[..(validIsfBytes.Length / 2)];

        using var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            var doc = new NoteDocument();
            doc.Pages.Add(new NotePage { Id = pageId, PageNumber = 1, IsfEntryPath = $"pages/{pageId:D}.isf" });

            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            {
                JsonSerializer.Serialize(stream, doc, InkSlateJsonContext.Default.NoteDocument);
            }

            var isfEntry = zip.CreateEntry($"pages/{pageId:D}.isf");
            using (var stream = isfEntry.Open())
            {
                stream.Write(truncatedBytes);
            }
        }

        zipMs.Position = 0;
        var loadedDoc = await FilePackageService.LoadAsync(zipMs);
        Assert.NotNull(loadedDoc);
        Assert.Single(loadedDoc.Pages);
        Assert.True(loadedDoc.PageStrokes.TryGetValue(pageId, out var recoveredStrokes));
        Assert.Empty(recoveredStrokes);

        // Part 2: CsCheck property fuzzing with random byte arrays inside ISF entry
        Gen.Byte.Array[1, 512].Sample(randomBytes =>
        {
            using var fuzzZipMs = new MemoryStream();
            using (var zip = new ZipArchive(fuzzZipMs, ZipArchiveMode.Create, leaveOpen: true))
            {
                var fuzzDoc = new NoteDocument();
                fuzzDoc.Pages.Add(new NotePage { Id = pageId, PageNumber = 1, IsfEntryPath = $"pages/{pageId:D}.isf" });

                var manifestEntry = zip.CreateEntry("manifest.json");
                using (var stream = manifestEntry.Open())
                {
                    JsonSerializer.Serialize(stream, fuzzDoc, InkSlateJsonContext.Default.NoteDocument);
                }

                var isfEntry = zip.CreateEntry($"pages/{pageId:D}.isf");
                using (var stream = isfEntry.Open())
                {
                    stream.Write(randomBytes);
                }
            }

            fuzzZipMs.Position = 0;
            var fuzzedLoadedDoc = FilePackageService.LoadAsync(fuzzZipMs).GetAwaiter().GetResult();
            Assert.NotNull(fuzzedLoadedDoc);
            Assert.True(fuzzedLoadedDoc.PageStrokes.TryGetValue(pageId, out var fuzzedStrokes));
            Assert.NotNull(fuzzedStrokes);
        });
    }

    [Fact]
    public async Task SaveAsync_RollbackOnFailure_CleansUpTempFile()
    {
        var filePath = Path.Combine(_testDirectory, $"cancelled_{Guid.NewGuid():N}.notex");

        var doc = new NoteDocument();
        doc.Pages.Add(new NotePage { PageNumber = 1 });

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FilePackageService.SaveAsync(doc, filePath, ct: cts.Token));

        Assert.False(File.Exists(filePath));

        var tmpFiles = Directory.GetFiles(_testDirectory, "*.tmp.*");
        Assert.Empty(tmpFiles);
    }

    [Fact]
    public void MainViewModel_SaveAndOpenCommands_WorkHeadless()
    {
        StaTestRunner.Run(() =>
        {
            var savePath = Path.Combine(_testDirectory, $"headless_{Guid.NewGuid():N}.notex");

            var vm1 = new MainViewModel();
            vm1.AddPageCommand.Execute(null);
            Assert.Equal(2, vm1.Pages.Count);

            // Add text element to page 2
            vm1.ActivePage!.AddTextElement(new System.Windows.Point(100, 100));
            var textVm = Assert.IsType<TextCanvasElementViewModel>(vm1.ActivePage.Elements[0]);
            textVm.Text = "Headless Test Text";

            // Add stroke to page 2
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(50, 50), new StylusPoint(150, 150) });
            vm1.ActivePage.Strokes!.Add(stroke);

            // Save via mocked picker
            vm1.FileSavePicker = () => savePath;
            vm1.SaveCommand.Execute(null);

            Assert.True(File.Exists(savePath));
            Assert.Equal(savePath, vm1.CurrentFilePath);

            // Create new fresh ViewModel and open saved file via mocked picker
            var vm2 = new MainViewModel();
            vm2.FileOpenPicker = () => savePath;
            vm2.OpenCommand.Execute(null);

            Assert.Equal(2, vm2.Pages.Count);
            Assert.Equal(savePath, vm2.CurrentFilePath);

            var loadedP2 = vm2.Pages[1];
            loadedP2.Hydrate();

            Assert.Single(loadedP2.Elements);
            var loadedText = Assert.IsType<TextCanvasElementViewModel>(loadedP2.Elements[0]);
            Assert.Equal("Headless Test Text", loadedText.Text);

            Assert.NotNull(loadedP2.Strokes);
            Assert.Single(loadedP2.Strokes);
            Assert.Equal(2, loadedP2.Strokes[0].StylusPoints.Count);
        });
    }

    [Fact]
    public void MainViewModel_DeleteStrokeOnActivePage_Save_Reopen_StrokeIsDeleted()
    {
        StaTestRunner.Run(() =>
        {
            var savePath = Path.Combine(_testDirectory, $"delete_stroke_active_{Guid.NewGuid():N}.notex");

            var vm1 = new MainViewModel();
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            vm1.ActivePage!.Hydrate();
            vm1.ActivePage.Strokes!.Add(stroke);
            Assert.Single(vm1.ActivePage.Strokes);

            // Save first time with stroke
            vm1.FileSavePicker = () => savePath;
            vm1.SaveCommand.Execute(null);
            Assert.True(File.Exists(savePath));

            // Now delete the stroke on active page
            vm1.ActivePage.SelectedStrokes = new StrokeCollection([stroke]);
            vm1.DeleteSelectedCommand.Execute(null);
            Assert.Empty(vm1.ActivePage.Strokes);

            // Save again after deletion
            vm1.SaveCommand.Execute(null);

            // Open in fresh MainViewModel (simulating app close and reopen)
            var vm2 = new MainViewModel();
            vm2.FileOpenPicker = () => savePath;
            vm2.OpenCommand.Execute(null);

            var page = vm2.ActivePage!;
            page.Hydrate();
            Assert.NotNull(page.Strokes);
            Assert.Empty(page.Strokes);
        });
    }

    [Fact]
    public void MainViewModel_DeleteStrokeOnDehydratedPage_Save_Reopen_StrokeIsDeleted()
    {
        StaTestRunner.Run(() =>
        {
            var savePath = Path.Combine(_testDirectory, $"delete_stroke_dehydrated_{Guid.NewGuid():N}.notex");

            var vm1 = new MainViewModel();
            vm1.AddPageCommand.Execute(null); // 2 pages
            Assert.Equal(2, vm1.Pages.Count);

            // Add stroke to page 1
            var page1 = vm1.Pages[0];
            page1.Hydrate();
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(30, 30), new StylusPoint(40, 40) });
            page1.Strokes!.Add(stroke);

            // Save first time
            vm1.FileSavePicker = () => savePath;
            vm1.SaveCommand.Execute(null);
            Assert.True(File.Exists(savePath));

            // Delete stroke from page 1
            page1.SelectedStrokes = new StrokeCollection([stroke]);
            vm1.DeleteSelectedCommand.Execute(null);
            Assert.Empty(page1.Strokes);

            // Navigate to page 2 -> page 1 is dehydrated
            vm1.ActivePage = vm1.Pages[1];
            page1.Dehydrate();
            Assert.False(page1.IsHydrated);

            // Save while page 1 is dehydrated
            vm1.SaveCommand.Execute(null);

            // Open in fresh MainViewModel (simulating app close and reopen)
            var vm2 = new MainViewModel();
            vm2.FileOpenPicker = () => savePath;
            vm2.OpenCommand.Execute(null);

            var loadedP1 = vm2.Pages[0];
            loadedP1.Hydrate();
            Assert.NotNull(loadedP1.Strokes);
            Assert.Empty(loadedP1.Strokes);
        });
    }

    [Fact]
    public void MainViewModel_LoadExistingFile_DeleteStroke_Save_Reopen_StrokeIsDeleted()
    {
        StaTestRunner.Run(() =>
        {
            var savePath = Path.Combine(_testDirectory, $"existing_file_delete_{Guid.NewGuid():N}.notex");

            // 1. Create initial file with a stroke and save it
            var vm1 = new MainViewModel();
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(100, 100), new StylusPoint(200, 200) });
            vm1.ActivePage!.Hydrate();
            vm1.ActivePage.Strokes!.Add(stroke);
            vm1.FileSavePicker = () => savePath;
            vm1.SaveCommand.Execute(null);
            Assert.True(File.Exists(savePath));

            // 2. Open the file in a new session (loads document.PageStrokes)
            var vm2 = new MainViewModel();
            vm2.FileOpenPicker = () => savePath;
            vm2.OpenCommand.Execute(null);

            var page = vm2.ActivePage!;
            page.Hydrate();
            Assert.Single(page.Strokes!);

            // 3. Delete the stroke
            page.SelectedStrokes = new StrokeCollection(page.Strokes);
            vm2.DeleteSelectedCommand.Execute(null);
            Assert.Empty(page.Strokes!);

            // 4. Save the modified document
            vm2.SaveCommand.Execute(null);

            // 5. Open in a third session (simulate reopening the app)
            var vm3 = new MainViewModel();
            vm3.FileOpenPicker = () => savePath;
            vm3.OpenCommand.Execute(null);

            var reopenedPage = vm3.ActivePage!;
            reopenedPage.Hydrate();
            Assert.NotNull(reopenedPage.Strokes);
            Assert.Empty(reopenedPage.Strokes!);
        });
    }

    [Fact]
    public void MainViewModel_ExistingFile_DeleteStroke_Dehydrate_Save_Reopen_StrokeIsDeleted()
    {
        StaTestRunner.Run(() =>
        {
            var savePath = Path.Combine(_testDirectory, $"existing_dehydrated_delete_{Guid.NewGuid():N}.notex");

            // 1. Create file with 2 pages and a stroke on page 1
            var vm1 = new MainViewModel();
            vm1.AddPageCommand.Execute(null);
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(50, 50), new StylusPoint(60, 60) });
            var page1 = vm1.Pages[0];
            page1.Hydrate();
            page1.Strokes!.Add(stroke);
            vm1.FileSavePicker = () => savePath;
            vm1.SaveCommand.Execute(null);
            Assert.True(File.Exists(savePath));

            // 2. Open existing file in session 2 (populates document.PageStrokes)
            var vm2 = new MainViewModel();
            vm2.FileOpenPicker = () => savePath;
            vm2.OpenCommand.Execute(null);

            var loadedPage1 = vm2.Pages[0];
            loadedPage1.Hydrate();
            Assert.Single(loadedPage1.Strokes!);

            // 3. Delete stroke on page 1
            loadedPage1.SelectedStrokes = new StrokeCollection(loadedPage1.Strokes);
            vm2.DeleteSelectedCommand.Execute(null);
            Assert.Empty(loadedPage1.Strokes!);

            // 4. Navigate away so page 1 dehydrates
            vm2.ActivePage = vm2.Pages[1];
            loadedPage1.Dehydrate();
            Assert.False(loadedPage1.IsHydrated);

            // 5. Save the modified document while page 1 is dehydrated
            vm2.SaveCommand.Execute(null);

            // 6. Open in session 3 (simulate reopening the app)
            var vm3 = new MainViewModel();
            vm3.FileOpenPicker = () => savePath;
            vm3.OpenCommand.Execute(null);

            var finalPage1 = vm3.Pages[0];
            finalPage1.Hydrate();
            Assert.NotNull(finalPage1.Strokes);
            Assert.Empty(finalPage1.Strokes!);
        });
    }

    [Fact]
    public void MainViewModel_ExistingFile_EraserRemovesStroke_Save_Reopen_StrokeIsDeleted()
    {
        StaTestRunner.Run(() =>
        {
            var savePath = Path.Combine(_testDirectory, $"eraser_remove_stroke_{Guid.NewGuid():N}.notex");

            // 1. Create file with 1 page and a stroke
            var vm1 = new MainViewModel();
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            var page = vm1.ActivePage!;
            page.Hydrate();
            page.Strokes!.Add(stroke);
            vm1.FileSavePicker = () => savePath;
            vm1.SaveCommand.Execute(null);

            // 2. Open in new session
            var vm2 = new MainViewModel();
            vm2.FileOpenPicker = () => savePath;
            vm2.OpenCommand.Execute(null);
            var loadedPage = vm2.ActivePage!;
            loadedPage.Hydrate();
            Assert.Single(loadedPage.Strokes!);

            // 3. Simulate eraser removing the stroke directly from Strokes
            loadedPage.Strokes!.Clear();
            Assert.True(loadedPage.IsDirty);
            Assert.True(vm2.IsDirty);

            // 4. Save
            vm2.SaveCommand.Execute(null);

            // 5. Reopen in session 3
            var vm3 = new MainViewModel();
            vm3.FileOpenPicker = () => savePath;
            vm3.OpenCommand.Execute(null);
            var reopenedPage = vm3.ActivePage!;
            reopenedPage.Hydrate();
            Assert.NotNull(reopenedPage.Strokes);
            Assert.Empty(reopenedPage.Strokes!);
        });
    }

    [Fact]
    public void MainViewModel_ExistingFile_PartialStrokeDelete_Save_Reopen_RemainingStrokePersists()
    {
        StaTestRunner.Run(() =>
        {
            var savePath = Path.Combine(_testDirectory, $"partial_stroke_delete_{Guid.NewGuid():N}.notex");

            // 1. Create file with 2 strokes
            var vm1 = new MainViewModel();
            var stroke1 = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            var stroke2 = new Stroke(new StylusPointCollection { new StylusPoint(30, 30), new StylusPoint(40, 40) });
            var page = vm1.ActivePage!;
            page.Hydrate();
            page.Strokes!.Add(stroke1);
            page.Strokes.Add(stroke2);
            vm1.FileSavePicker = () => savePath;
            vm1.SaveCommand.Execute(null);

            // 2. Open in new session
            var vm2 = new MainViewModel();
            vm2.FileOpenPicker = () => savePath;
            vm2.OpenCommand.Execute(null);
            var loadedPage = vm2.ActivePage!;
            loadedPage.Hydrate();
            Assert.Equal(2, loadedPage.Strokes!.Count);

            // 3. Delete stroke1 only
            loadedPage.SelectedStrokes = new StrokeCollection([loadedPage.Strokes![0]]);
            vm2.DeleteSelectedCommand.Execute(null);
            Assert.Single(loadedPage.Strokes!);

            // 4. Save
            vm2.SaveCommand.Execute(null);

            // 5. Reopen in session 3
            var vm3 = new MainViewModel();
            vm3.FileOpenPicker = () => savePath;
            vm3.OpenCommand.Execute(null);
            var reopenedPage = vm3.ActivePage!;
            reopenedPage.Hydrate();
            Assert.NotNull(reopenedPage.Strokes);
            Assert.Single(reopenedPage.Strokes!);
        });
    }
}
