using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Ink;
using InkSlate.Core.Models;

namespace InkSlate.Core.Services;

/// <summary>
/// Provides secure, isolated local crash recovery snapshot management for NoteDocuments.
/// Persists dirty session states to %LOCALAPPDATA%\InkSlate\Recovery\{DocumentId}.notex.recovery
/// with restrictive current-user-only access control lists (ACLs).
/// </summary>
public static class CrashRecoveryService
{
    public const string RecoveryFileExtension = ".notex.recovery";

    public static string DefaultRecoveryDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InkSlate", "Recovery");

    public static string GetRecoveryFilePath(Guid documentId, string? recoveryDirectory = null)
    {
        var dir = string.IsNullOrWhiteSpace(recoveryDirectory) ? DefaultRecoveryDirectory : recoveryDirectory;
        return Path.Combine(dir, $"{documentId:D}{RecoveryFileExtension}");
    }

    /// <summary>
    /// Ensures the recovery directory exists and configures current-user-only restrictive ACLs on Windows.
    /// </summary>
    public static void EnsureSecureRecoveryDirectory(string? recoveryDirectory = null)
    {
        var dir = string.IsNullOrWhiteSpace(recoveryDirectory) ? DefaultRecoveryDirectory : recoveryDirectory;
        Directory.CreateDirectory(dir);

        if (OperatingSystem.IsWindows())
        {
            ApplyCurrentUserOnlyDirectoryAcl(dir);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyCurrentUserOnlyDirectoryAcl(string directoryPath)
    {
        try
        {
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser is null)
            {
                return;
            }

            var dirInfo = new DirectoryInfo(directoryPath);
            var security = new DirectorySecurity();

            // Disable inheritance and remove existing inherited access rules
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // Grant current user FullControl over this container and child files/subdirectories
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(security);
        }
        catch (PlatformNotSupportedException)
        {
            // Fallback for non-Windows or environments without ACL support
        }
        catch (UnauthorizedAccessException)
        {
            // If ACL modification is restricted in sandbox, directory creation was already completed
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyCurrentUserOnlyFileAcl(string filePath)
    {
        try
        {
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser is null)
            {
                return;
            }

            var fileInfo = new FileInfo(filePath);
            var security = new FileSecurity();

            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            fileInfo.SetAccessControl(security);
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Atomically writes an isolated recovery package snapshot using FilePackageService.
    /// </summary>
    public static async Task<string> WriteRecoverySnapshotAsync(
        NoteDocument document,
        string? recoveryDirectory = null,
        Func<Guid, StrokeCollection?>? strokesProvider = null,
        Func<Guid, byte[]?>? isfBytesProvider = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        EnsureSecureRecoveryDirectory(recoveryDirectory);
        var recoveryPath = GetRecoveryFilePath(document.Id, recoveryDirectory);

        await FilePackageService.SaveAsync(document, recoveryPath, strokesProvider, isfBytesProvider, ct).ConfigureAwait(false);

        if (OperatingSystem.IsWindows() && File.Exists(recoveryPath))
        {
            ApplyCurrentUserOnlyFileAcl(recoveryPath);
        }

        return recoveryPath;
    }

    /// <summary>
    /// Deletes the recovery file for the specified document ID if present.
    /// </summary>
    public static bool DeleteRecoveryFile(Guid documentId, string? recoveryDirectory = null)
    {
        try
        {
            var recoveryPath = GetRecoveryFilePath(documentId, recoveryDirectory);
            if (File.Exists(recoveryPath))
            {
                File.Delete(recoveryPath);
                return true;
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup
        }

        return false;
    }
}

