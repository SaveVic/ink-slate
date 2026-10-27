namespace InkSlate.Core.Models;

public sealed class UserSettings
{
    public List<string> CustomColorHexes { get; set; } = [];
    public List<CustomPenPreset> CustomPenPresets { get; set; } = [];
}

