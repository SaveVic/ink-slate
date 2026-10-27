using System.IO;

namespace InkSlate.Core.Services;

public static class StartupArgumentResolver
{
    public const string NotexExtension = ".notex";

    public static bool TryResolveNotexFilePath(IReadOnlyList<string>? args, out string? fullPath)
    {
        fullPath = null;
        if (args is null || args.Count == 0)
        {
            return false;
        }

        var candidate = args[0]?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        // Strip enclosing quotes if present
        if (candidate.Length >= 2 && candidate.StartsWith('"') && candidate.EndsWith('"'))
        {
            candidate = candidate[1..^1].Trim();
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            var resolved = Path.GetFullPath(candidate);
            if (!string.Equals(Path.GetExtension(resolved), NotexExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!File.Exists(resolved))
            {
                return false;
            }

            fullPath = resolved;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}

