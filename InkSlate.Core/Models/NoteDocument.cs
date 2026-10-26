namespace InkSlate.Core.Models;

public sealed class NoteDocument
{
    public const int CurrentFormatVersion = 1;
    public const string DefaultTitle = "Untitled";

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public string Title { get; set; } = DefaultTitle;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public double DefaultPageWidth { get; set; } = PageDimensions.A4.Width;
    public double DefaultPageHeight { get; set; } = PageDimensions.A4.Height;
    public List<NotePage> Pages { get; init; } = [];
}

