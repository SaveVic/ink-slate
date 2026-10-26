namespace InkSlate.Core.Models.CanvasElements;

public sealed class ImageCanvasElement : CanvasElement
{
    /// <summary>
    /// Relative asset key inside the .notex archive (e.g., "assets/3f2504e0-...png").
    /// </summary>
    public required string AssetKey { get; set; }

    public double CropX { get; set; }
    public double CropY { get; set; }
    public double CropWidth { get; set; }
    public double CropHeight { get; set; }
}

