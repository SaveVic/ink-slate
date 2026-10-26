using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.Models;

public sealed class NotePage
{
    public const string DefaultBackgroundHex = "#FFFFFFFF";

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public int PageNumber { get; set; } = 1;
    public double Width { get; set; } = PageDimensions.A4.Width;
    public double Height { get; set; } = PageDimensions.A4.Height;
    public string BackgroundHex { get; set; } = DefaultBackgroundHex;
    public string? IsfEntryPath { get; set; }
    public List<CanvasElement> Elements { get; init; } = [];
}

