using System.Globalization;
using System.IO;
using System.Text.Json;
using InkSlate.Core.Models;
using InkSlate.Core.Serialization;

namespace InkSlate.Core.Services;

public sealed class UserSettingsService
{
    public const string DefaultSettingsFileName = "user-settings.json";
    public const string SettingsFolderName = "InkSlate";

    public UserSettingsService(string? settingsFilePathOverride = null)
    {
        SettingsFilePath = !string.IsNullOrWhiteSpace(settingsFilePathOverride)
            ? settingsFilePathOverride
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsFolderName,
                DefaultSettingsFileName);
    }

    public string SettingsFilePath { get; }

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new UserSettings();
            }

            var jsonBytes = File.ReadAllBytes(SettingsFilePath);
            if (jsonBytes.Length == 0)
            {
                return new UserSettings();
            }

            var settings = JsonSerializer.Deserialize(jsonBytes, InkSlateJsonContext.Default.UserSettings);
            if (settings is null)
            {
                return new UserSettings();
            }

            Sanitize(settings);
            return settings;
        }
        catch (Exception)
        {
            // Fall back to clean default settings if file is corrupted, inaccessible, or invalid JSON
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Sanitize(settings);

        var dir = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = $"{SettingsFilePath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, InkSlateJsonContext.Default.UserSettings);
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, SettingsFilePath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            throw;
        }
    }

    public static bool TryNormalizeColorHex(string? input, out string normalizedHex, out byte r, out byte g, out byte b)
    {
        normalizedHex = string.Empty;
        r = g = b = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 6)
        {
            if (byte.TryParse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
                byte.TryParse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
                byte.TryParse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
            {
                normalizedHex = string.Create(CultureInfo.InvariantCulture, $"#FF{r:X2}{g:X2}{b:X2}");
                return true;
            }
        }
        else if (trimmed.Length == 8)
        {
            if (byte.TryParse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a) &&
                byte.TryParse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
                byte.TryParse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
                byte.TryParse(trimmed.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
            {
                normalizedHex = string.Create(CultureInfo.InvariantCulture, $"#{a:X2}{r:X2}{g:X2}{b:X2}");
                return true;
            }
        }

        return false;
    }

    public static void Sanitize(UserSettings settings, IEnumerable<string>? excludedDefaultHexes = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var excludedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (excludedDefaultHexes is not null)
        {
            foreach (var hex in excludedDefaultHexes)
            {
                if (TryNormalizeColorHex(hex, out var norm, out _, out _, out _))
                {
                    excludedSet.Add(norm);
                }
            }
        }

        var sanitizedHexes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (settings.CustomColorHexes is not null)
        {
            foreach (var raw in settings.CustomColorHexes)
            {
                if (TryNormalizeColorHex(raw, out var normalized, out _, out _, out _))
                {
                    if (!excludedSet.Contains(normalized) && seen.Add(normalized))
                    {
                        sanitizedHexes.Add(normalized);
                    }
                }
            }
        }

        settings.CustomColorHexes = sanitizedHexes;

        if (settings.CustomPenPresets is not null)
        {
            var sanitizedPresets = new List<CustomPenPreset>();
            foreach (var preset in settings.CustomPenPresets)
            {
                if (preset is null)
                {
                    continue;
                }

                if (preset.Id == Guid.Empty)
                {
                    preset.Id = Guid.CreateVersion7();
                }

                preset.Name = !string.IsNullOrWhiteSpace(preset.Name)
                    ? preset.Name.Trim()
                    : "Custom Pen";
                if (preset.Name.Length > 64)
                {
                    preset.Name = preset.Name[..64];
                }

                if (!TryNormalizeColorHex(preset.ColorHex, out var normColor, out _, out _, out _))
                {
                    normColor = "#FF1E1E1E";
                }
                preset.ColorHex = normColor;

                if (!double.IsFinite(preset.ThicknessDip) || preset.ThicknessDip <= 0.0)
                {
                    preset.ThicknessDip = 2.5;
                }
                else
                {
                    preset.ThicknessDip = Math.Clamp(preset.ThicknessDip, 0.5, 64.0);
                }

                preset.Shortcut = preset.Shortcut?.Trim() ?? string.Empty;
                if (preset.Shortcut.Length > 32)
                {
                    preset.Shortcut = preset.Shortcut[..32];
                }

                sanitizedPresets.Add(preset);
            }

            settings.CustomPenPresets = sanitizedPresets;
        }
        else
        {
            settings.CustomPenPresets = [];
        }
    }
}

