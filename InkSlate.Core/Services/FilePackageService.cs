using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Ink;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Serialization;

namespace InkSlate.Core.Services;

/// <summary>
/// Provides secure, atomic read and write operations for .notex ZIP document packages.
/// Enforces strict path allowlists (Zip Slip prevention), per-entry/cumulative decompression quotas (Zip Bomb prevention),
/// and defensive inking deserialization.
/// </summary>
public static class FilePackageService
{
    public const long MaxArchiveFileSizeBytes = 500 * 1024 * 1024; // 500 MB
    public const int MaxArchiveEntryCount = 2000;
    public const long MaxManifestSizeBytes = 5 * 1024 * 1024;      // 5 MB
    public const long MaxPageIsfSizeBytes = 15 * 1024 * 1024;     // 15 MB
    public const long MaxAssetSizeBytes = 25 * 1024 * 1024;       // 25 MB
    public const long MaxCumulativeUncompressedBytes = 500 * 1024 * 1024; // 500 MB

    public const string ManifestEntryName = "manifest.json";

    /// <summary>
    /// Atomically saves the note document and all associated ISF strokes and media assets into a .notex package.
    /// Writes to a temporary sibling file and commits via atomic file move.
    /// </summary>
    public static async Task SaveAsync(
        NoteDocument document,
        string targetPath,
        Func<Guid, StrokeCollection?>? strokesProvider = null,
        Func<Guid, byte[]?>? isfBytesProvider = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var targetFullPath = Path.GetFullPath(targetPath);
        var directory = Path.GetDirectoryName(targetFullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{targetFullPath}.tmp.{Guid.NewGuid():N}";

        try
        {
            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    // 1. Write strokes per page into pages/{guid}.isf
                    foreach (var page in document.Pages)
                    {
                        ct.ThrowIfCancellationRequested();

                        var entryPath = $"pages/{page.Id:D}.isf";
                        page.IsfEntryPath = entryPath;

                        var isfEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                        await using var isfStream = isfEntry.Open();

                        var strokes = strokesProvider?.Invoke(page.Id);
                        if (strokes is not null)
                        {
                            strokes.Save(isfStream, compress: true);
                        }
                        else
                        {
                            var bytes = isfBytesProvider?.Invoke(page.Id);
                            if (bytes is { Length: > 0 })
                            {
                                await isfStream.WriteAsync(bytes, ct).ConfigureAwait(false);
                            }
                            else if (document.PageStrokes.TryGetValue(page.Id, out var fallbackStrokes) && fallbackStrokes.Count > 0)
                            {
                                fallbackStrokes.Save(isfStream, compress: true);
                            }
                            else
                            {
                                new StrokeCollection().Save(isfStream, compress: true);
                            }
                        }
                    }

                    // 2. Write manifest.json
                    ct.ThrowIfCancellationRequested();
                    var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                    await using (var manifestStream = manifestEntry.Open())
                    {
                        await JsonSerializer.SerializeAsync(
                            manifestStream,
                            document,
                            InkSlateJsonContext.Default.NoteDocument,
                            ct).ConfigureAwait(false);
                    }

                    // 3. Write deduplicated media assets without compression
                    var assetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var page in document.Pages)
                    {
                        foreach (var el in page.Elements)
                        {
                            if (el is ImageCanvasElement img && !string.IsNullOrWhiteSpace(img.AssetKey))
                            {
                                assetKeys.Add(img.AssetKey);
                            }
                        }
                    }

                    foreach (var key in document.Assets.Keys)
                    {
                        assetKeys.Add(key);
                    }

                    foreach (var assetKey in assetKeys)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (document.Assets.TryGetValue(assetKey, out var assetBytes) && assetBytes is { Length: > 0 })
                        {
                            var assetEntry = archive.CreateEntry(assetKey, CompressionLevel.NoCompression);
                            await using var assetStream = assetEntry.Open();
                            await assetStream.WriteAsync(assetBytes, ct).ConfigureAwait(false);
                        }
                    }
                }
            }

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, targetFullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup error on failed save
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Loads a .notex document package from disk, enforcing archive size limits, entry counts,
    /// strict path security allowlists, bounded decompression streams, and defensive ink recovery.
    /// </summary>
    public static async Task<NoteDocument> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Note package file not found: {fullPath}", fullPath);
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxArchiveFileSizeBytes)
        {
            throw new InvalidDataException(
                $"Package file size ({fileInfo.Length} bytes) exceeds maximum allowed size of {MaxArchiveFileSizeBytes / (1024 * 1024)} MB.");
        }

        await using var fileStream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count > MaxArchiveEntryCount)
        {
            throw new InvalidDataException(
                $"Archive contains {archive.Entries.Count} entries, exceeding maximum allowed limit of {MaxArchiveEntryCount}.");
        }

        var byteTracker = new ArchiveByteTracker(MaxCumulativeUncompressedBytes);

        ZipArchiveEntry? manifestEntry = null;
        var isfEntries = new Dictionary<Guid, ZipArchiveEntry>();
        var assetEntries = new List<ZipArchiveEntry>();

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var name = entry.FullName;

            // Zip Slip and invalid path rejection
            if (name.Contains("..", StringComparison.Ordinal) ||
                name.StartsWith('/') ||
                name.StartsWith('\\') ||
                name.Contains(':'))
            {
                throw new InvalidDataException(
                    $"Archive entry '{name}' violates path security policy (Zip Slip / illegal characters).");
            }

            if (name.Equals(ManifestEntryName, StringComparison.Ordinal))
            {
                manifestEntry = entry;
            }
            else if (name.EndsWith('/') && (name == "pages/" || name == "assets/"))
            {
                // Harmless folder marker
                continue;
            }
            else if (DocumentValidator.IsValidIsfEntryPath(name))
            {
                var fileName = Path.GetFileNameWithoutExtension(name);
                if (Guid.TryParse(fileName, out var pageId))
                {
                    isfEntries[pageId] = entry;
                }
            }
            else if (DocumentValidator.IsValidAssetKey(name))
            {
                assetEntries.Add(entry);
            }
            else
            {
                throw new InvalidDataException($"Archive contains disallowed entry: '{name}'.");
            }
        }

        if (manifestEntry is null)
        {
            throw new InvalidDataException("Archive does not contain required manifest.json.");
        }

        // 1. Read and deserialize manifest.json
        NoteDocument document;
        await using (var rawStream = manifestEntry.Open())
        await using (var boundedStream = new BoundedReadStream(rawStream, MaxManifestSizeBytes, byteTracker))
        {
            var deserialized = await JsonSerializer.DeserializeAsync(
                boundedStream,
                InkSlateJsonContext.Default.NoteDocument,
                ct).ConfigureAwait(false);

            if (deserialized is null)
            {
                throw new InvalidDataException("Failed to deserialize manifest.json from package.");
            }

            document = DocumentValidator.Sanitize(deserialized);
        }

        // 2. Load media assets
        foreach (var assetEntry in assetEntries)
        {
            ct.ThrowIfCancellationRequested();
            await using var rawStream = assetEntry.Open();
            await using var boundedStream = new BoundedReadStream(rawStream, MaxAssetSizeBytes, byteTracker);
            using var ms = new MemoryStream();
            await boundedStream.CopyToAsync(ms, ct).ConfigureAwait(false);
            document.Assets[assetEntry.FullName] = ms.ToArray();
        }

        // 3. Load ISF strokes per page defensively
        foreach (var page in document.Pages)
        {
            ct.ThrowIfCancellationRequested();

            StrokeCollection strokes;
            if (isfEntries.TryGetValue(page.Id, out var isfEntry))
            {
                try
                {
                    await using var rawStream = isfEntry.Open();
                    await using var boundedStream = new BoundedReadStream(rawStream, MaxPageIsfSizeBytes, byteTracker);
                    using var ms = new MemoryStream();
                    await boundedStream.CopyToAsync(ms, ct).ConfigureAwait(false);
                    ms.Position = 0;
                    strokes = new StrokeCollection(ms);
                }
                catch (ArgumentException)
                {
                    // Defensive recovery: Corrupted ISF data safely yields empty strokes
                    strokes = [];
                }
                catch (Exception ex) when (ex is not InvalidDataException and not OutOfMemoryException)
                {
                    strokes = [];
                }
            }
            else
            {
                strokes = [];
            }

            document.PageStrokes[page.Id] = strokes;
        }

        return document;
    }
}
