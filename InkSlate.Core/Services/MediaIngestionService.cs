using System.IO;
using System.Windows.Media.Imaging;
using InkSlate.Core.Serialization;

namespace InkSlate.Core.Services;

public enum ImageFormat
{
    Png,
    Jpeg,
    Bmp
}

public sealed record IngestedMediaResult(
    string AssetKey,
    byte[] SanitizedBytes,
    BitmapSource FrozenBitmap,
    double PixelWidth,
    double PixelHeight);

public static class MediaIngestionService
{
    public const long MaxImageFileSize = 25 * 1024 * 1024; // 25 MB
    public const int MaxDecodePixelDimension = 2560;

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] BmpHeader = [0x42, 0x4D];
    private static readonly byte[] ExecutableHeader = [0x4D, 0x5A]; // MZ

    public static bool TryDetectImageFormat(ReadOnlySpan<byte> header, out ImageFormat format)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual(PngHeader))
        {
            format = ImageFormat.Png;
            return true;
        }

        if (header.Length >= 3 && header[..3].SequenceEqual(JpegHeader))
        {
            format = ImageFormat.Jpeg;
            return true;
        }

        if (header.Length >= 2 && header[..2].SequenceEqual(BmpHeader))
        {
            format = ImageFormat.Bmp;
            return true;
        }

        format = default;
        return false;
    }

    public static IngestedMediaResult IngestBytes(byte[] rawBytes, string? preferredKey = null)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);

        if (rawBytes.Length > MaxImageFileSize)
        {
            throw new InvalidDataException($"Image exceeds maximum allowed size of {MaxImageFileSize / (1024 * 1024)} MB.");
        }

        if (rawBytes.Length < 8)
        {
            throw new InvalidDataException("Image payload is too small or truncated.");
        }

        var headerSpan = rawBytes.AsSpan(0, Math.Min(rawBytes.Length, 8));
        if (headerSpan.Length >= 2 && headerSpan[..2].SequenceEqual(ExecutableHeader))
        {
            throw new InvalidDataException("Executable files (MZ header) are strictly forbidden.");
        }

        if (!TryDetectImageFormat(headerSpan, out var format))
        {
            throw new InvalidDataException("Unsupported or corrupted image format. Only PNG, JPEG, and BMP are permitted.");
        }

        using var inStream = new MemoryStream(rawBytes, writable: false);
        return ProcessImageStream(inStream, rawBytes, format, preferredKey);
    }

    public static IngestedMediaResult IngestStream(Stream stream, string? preferredKey = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += read;
            if (totalRead > MaxImageFileSize)
            {
                throw new InvalidDataException($"Image exceeds maximum allowed size of {MaxImageFileSize / (1024 * 1024)} MB.");
            }

            memory.Write(buffer, 0, read);
        }

        return IngestBytes(memory.ToArray(), preferredKey);
    }

    public static IngestedMediaResult IngestFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Image file not found.", filePath);
        }

        if (fileInfo.Length > MaxImageFileSize)
        {
            throw new InvalidDataException($"Image exceeds maximum allowed size of {MaxImageFileSize / (1024 * 1024)} MB.");
        }

        using var fs = fileInfo.OpenRead();
        return IngestStream(fs);
    }

    public static IngestedMediaResult IngestBitmapSource(BitmapSource bitmap, string? preferredKey = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        var bytes = ms.ToArray();

        return IngestBytes(bytes, preferredKey);
    }

    private static IngestedMediaResult ProcessImageStream(
        MemoryStream stream,
        byte[] rawBytes,
        ImageFormat format,
        string? preferredKey)
    {
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.DelayCreation,
            BitmapCacheOption.None);

        if (decoder.Frames.Count == 0)
        {
            throw new InvalidDataException("Image contains no valid frames.");
        }

        var frame = decoder.Frames[0];
        var rawWidth = frame.PixelWidth;
        var rawHeight = frame.PixelHeight;

        if (rawWidth <= 0 || rawHeight <= 0)
        {
            throw new InvalidDataException("Image has invalid dimensions.");
        }

        stream.Position = 0;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;

        if (rawWidth > MaxDecodePixelDimension || rawHeight > MaxDecodePixelDimension)
        {
            if (rawWidth >= rawHeight)
            {
                bitmap.DecodePixelWidth = MaxDecodePixelDimension;
            }
            else
            {
                bitmap.DecodePixelHeight = MaxDecodePixelDimension;
            }
        }

        bitmap.EndInit();
        bitmap.Freeze();

        byte[] finalBytes;
        string extension;

        if (format == ImageFormat.Bmp)
        {
            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var transcodeMs = new MemoryStream();
            pngEncoder.Save(transcodeMs);
            finalBytes = transcodeMs.ToArray();
            extension = "png";
        }
        else
        {
            finalBytes = rawBytes;
            extension = format == ImageFormat.Jpeg ? "jpg" : "png";
        }

        var assetKey = !string.IsNullOrWhiteSpace(preferredKey) && DocumentValidator.IsValidAssetKey(preferredKey)
            ? preferredKey
            : $"assets/{Guid.NewGuid():D}.{extension}";

        return new IngestedMediaResult(assetKey, finalBytes, bitmap, bitmap.PixelWidth, bitmap.PixelHeight);
    }
}

