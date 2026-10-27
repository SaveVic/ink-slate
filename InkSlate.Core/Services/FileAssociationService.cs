using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace InkSlate.Core.Services;

[SupportedOSPlatform("windows")]
public static class FileAssociationService
{
    public const string Extension = ".notex";
    public const string ProgId = "InkSlate.Document";
    public const string FriendlyTypeName = "InkSlate Document";

    public static void RegisterCurrentUserAssociation(string executablePath, RegistryKey? classesRootOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var fullExePath = Path.GetFullPath(executablePath);

        using var classesKey = classesRootOverride is not null
            ? null
            : Registry.CurrentUser.CreateSubKey(@"Software\Classes");

        var targetClasses = classesRootOverride ?? classesKey;
        if (targetClasses is null)
        {
            return;
        }

        // 1. Register extension mapping: .notex -> InkSlate.Document
        using (var extKey = targetClasses.CreateSubKey(Extension))
        {
            extKey.SetValue(string.Empty, ProgId, RegistryValueKind.String);
        }

        // 2. Register ProgId with friendly name, icon, and open verb
        using (var progIdKey = targetClasses.CreateSubKey(ProgId))
        {
            progIdKey.SetValue(string.Empty, FriendlyTypeName, RegistryValueKind.String);

            using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
            {
                iconKey.SetValue(string.Empty, $"\"{fullExePath}\",0", RegistryValueKind.String);
            }

            using (var shellKey = progIdKey.CreateSubKey(@"shell\open\command"))
            {
                shellKey.SetValue(string.Empty, $"\"{fullExePath}\" \"%1\"", RegistryValueKind.String);
            }
        }
    }

    public static void UnregisterCurrentUserAssociation(RegistryKey? classesRootOverride = null)
    {
        using var classesKey = classesRootOverride is not null
            ? null
            : Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);

        var targetClasses = classesRootOverride ?? classesKey;
        if (targetClasses is null)
        {
            return;
        }

        try
        {
            targetClasses.DeleteSubKeyTree(Extension, throwOnMissingSubKey: false);
        }
        catch
        {
            // Ignore missing subkey on cleanup
        }

        try
        {
            targetClasses.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
        }
        catch
        {
            // Ignore missing subkey on cleanup
        }
    }
}

