using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Common.Configuration;

/// <summary>
/// Writes configuration files through a protected temporary file.
/// </summary>
internal static class ProtectedConfigurationWriter
{
    /// <summary>
    /// Persists configuration without exposing partial or unprotected contents.
    /// </summary>
    /// <param name="configuration">The configuration object.</param>
    /// <param name="path">The destination path.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    /// <param name="logger">Optional logger for cleanup or permission errors.</param>
    /// <param name="restrictExistingWindowsAcl">Whether to restrict an existing Windows destination before replacement.</param>
    public static void Persist(object configuration, string path, IXmlSerializer xmlSerializer, ILogger? logger = null, bool restrictExistingWindowsAcl = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(xmlSerializer);

        PersistCore(path, temporaryPath => xmlSerializer.SerializeToFile(configuration, temporaryPath), logger, restrictExistingWindowsAcl);
    }

    /// <summary>
    /// Persists XML through a protected temporary file using a caller-provided stream writer.
    /// </summary>
    /// <param name="path">The destination path.</param>
    /// <param name="serialize">The XML stream writer.</param>
    /// <param name="logger">Optional logger for cleanup or permission errors.</param>
    /// <param name="restrictExistingWindowsAcl">Whether to restrict an existing Windows destination before replacement.</param>
    public static void PersistXml(string path, Action<Stream> serialize, ILogger? logger = null, bool restrictExistingWindowsAcl = false)
    {
        ArgumentNullException.ThrowIfNull(serialize);

        PersistCore(
            path,
            temporaryPath =>
            {
                using var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.Write, FileShare.None);
                serialize(stream);
            },
            logger,
            restrictExistingWindowsAcl);
    }

    /// <summary>
    /// Persists text through a protected temporary file.
    /// </summary>
    /// <param name="text">The text to persist.</param>
    /// <param name="path">The destination path.</param>
    /// <param name="logger">Optional logger for cleanup or permission errors.</param>
    /// <param name="restrictExistingWindowsAcl">Whether to restrict an existing Windows destination before replacement.</param>
    /// <param name="failClosedUnixPermissions">Whether to fail without promoting when POSIX permissions cannot be restricted.</param>
    /// <param name="cancellationToken">Token checked before writing and immediately before promotion.</param>
    public static void PersistText(
        string text,
        string path,
        ILogger? logger = null,
        bool restrictExistingWindowsAcl = false,
        bool failClosedUnixPermissions = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        PersistCore(
            path,
            temporaryPath =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.WriteAllText(temporaryPath, text);
            },
            logger,
            restrictExistingWindowsAcl,
            failClosedUnixPermissions,
            cancellationToken.ThrowIfCancellationRequested);
    }

    private static void PersistCore(
        string path,
        Action<string> serialize,
        ILogger? logger,
        bool restrictExistingWindowsAcl,
        bool failClosedUnixPermissions = false,
        Action? beforePromotion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Path can't be a root directory."));

        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
            }

            // Protect the empty file before the serializer can write secrets into it.
            RestrictFilePermissions(temporaryPath, logger, failClosedUnixPermissions);
            RestrictWindowsFilePermissions(temporaryPath);
            serialize(temporaryPath);

            if (OperatingSystem.IsWindows() && File.Exists(path))
            {
                if (restrictExistingWindowsAcl)
                {
                    RestrictWindowsFilePermissions(path, setOwner: false);
                }

                beforePromotion?.Invoke();
                var backupPath = temporaryPath + ".bak";
                try
                {
                    File.Replace(temporaryPath, path, backupPath);
                }
                catch
                {
                    try
                    {
                        RestoreBackupIfDestinationMissing(path, backupPath);
                    }
                    catch (Exception recoveryException) when (recoveryException is IOException or UnauthorizedAccessException)
                    {
                        logger?.LogCritical(recoveryException, "Unable to restore configuration backup after replacement failed: {Path}", backupPath);
                    }

                    throw;
                }
                finally
                {
                    if (File.Exists(path))
                    {
                        TryDeleteTemporaryFile(backupPath, logger);
                    }
                }
            }
            else
            {
                beforePromotion?.Invoke();
                File.Move(temporaryPath, path, overwrite: true);
            }
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath, logger);
        }
    }

    /// <summary>
    /// Restores a configuration backup only when the destination is absent.
    /// </summary>
    /// <param name="path">The destination path.</param>
    /// <param name="backupPath">The backup path.</param>
    public static void RestoreBackupIfDestinationMissing(string path, string backupPath)
    {
        if (!File.Exists(path) && File.Exists(backupPath))
        {
            File.Move(backupPath, path);
        }
    }

    private static void RestrictWindowsFilePermissions(string path, bool setOwner = true)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var serviceIdentity = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows identity has no SID.");
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        if (setOwner)
        {
            security.SetOwner(serviceIdentity);
        }
        security.AddAccessRule(new FileSystemAccessRule(serviceIdentity, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    private static void TryDeleteTemporaryFile(string path, ILogger? logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Unable to remove temporary configuration file: {Path}", path);
        }
    }

    private static void RestrictFilePermissions(string path, ILogger? logger, bool failClosed)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (!failClosed && (ex is IOException or UnauthorizedAccessException))
        {
            if (logger is null)
            {
                throw;
            }

            logger.LogWarning(ex, "Unable to restrict permissions on configuration file: {Path}", path);
        }
    }
}
