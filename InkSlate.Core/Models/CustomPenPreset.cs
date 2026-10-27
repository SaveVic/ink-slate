namespace InkSlate.Core.Models;

public sealed class CustomPenPreset
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "Custom Pen";
    public string ColorHex { get; set; } = "#FFDC2626";
    public double ThicknessDip { get; set; } = 2.5;
    public string Shortcut { get; set; } = string.Empty;
}

