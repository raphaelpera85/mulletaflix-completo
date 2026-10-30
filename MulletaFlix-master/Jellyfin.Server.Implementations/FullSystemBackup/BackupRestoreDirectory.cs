using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MulletaFlix.Server.Implementations.FullSystemBackup;

/// <summary>
/// Provides single-component filesystem operations relative to an opened directory.
/// Root-path components are opened without following links; writes require ACL validation of the target directory.
/// </summary>
internal sealed class BackupRestoreDirectory : IDisposable
{
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileReadControl = 0x00020000;
    private const uint FileListDirectory = 0x00000001;
    private const uint FileAddFile = 0x00000002;
    private const uint FileAddSubdirectory = 0x00000004;
    private const uint FileTraverse = 0x00000020;
    private const uint DeleteAccess = 0x00010000;
    private const uint SynchronizeAccess = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileOpen = 1;
    private const uint FileCreate = 2;
    private const uint FileOpenIf = 3;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileWriteThrough = 0x00000002;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int FileAttributeTagInfoClass = 9;
    private const int FileIdInfoClass = 18;
    private const int FileRenameInformationClass = 10;
    private const int FileDispositionInfoClass = 4;

    private const int LinuxAtRemovedir = 0x0200;
    private const uint LinuxRenameNoReplace = 1;
    private const int LinuxAtSymlinkNoFollow = 0x0100;
    private const int LinuxAtEmptyPath = 0x1000;
    private const uint LinuxStatxType = 0x00000001;
    private const uint LinuxStatxInode = 0x00000100;
    private const uint LinuxStatxMountId = 0x00001000;
    private const ushort LinuxFileTypeMask = 0xF000;
    private const ushort LinuxFileTypeRegular = 0x8000;
    private const ushort LinuxFileTypeDirectory = 0x4000;
    private const int LinuxOpenWriteOnly = 1;
    private const int LinuxOpenCreate = 0x40;
    private const int LinuxOpenExclusive = 0x80;
    private const int LinuxOpenNonBlock = 0x800;
    private const int LinuxOpenDirectory = 0x10000;
    private const int LinuxOpenNoFollow = 0x20000;
    private const int LinuxOpenCloseOnExec = 0x80000;
    private const int LinuxOpenPath = 0x200000;
    private const ushort LinuxFileTypeSymbolicLink = 0xA000;

    private readonly SafeFileHandle _handle;
    private readonly SafeFileHandle[] _ancestorHandles;
    private readonly bool _isWindows;

    private BackupRestoreDirectory(SafeFileHandle handle, bool isWindows, SafeFileHandle[]? ancestorHandles = null)
    {
        _handle = handle;
        _isWindows = isWindows;
        _ancestorHandles = ancestorHandles ?? [];
    }

    /// <summary>
    /// Opens a trusted root directory. The configured root path is the trust boundary.
    /// </summary>
    /// <param name="path">Absolute root directory path.</param>
    /// <returns>An opened directory authority.</returns>
    public static BackupRestoreDirectory OpenRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);

        if (OperatingSystem.IsWindows())
        {
            var pathRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(pathRoot))
            {
                throw new IOException("Restore root must have a filesystem root.");
            }

            var handle = CreateFileW(
                pathRoot,
                FileListDirectory | FileTraverse | FileReadAttributes | FileReadControl | SynchronizeAccess,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                3,
                0x02000000 | 0x00200000,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastPInvokeError();
                handle.Dispose();
                throw new Win32Exception(error, "Failed to open restore root directory safely.");
            }

            var ancestors = new List<SafeFileHandle>();
            SafeFileHandle? current = handle;
            try
            {
                EnsureWindowsDirectory(current, "Restore root must be a real directory, not a reparse point.");
                var components = fullPath[pathRoot.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
                if (components.Length == 0)
                {
                    throw new IOException("Restore root cannot be a volume or share root.");
                }

                for (var index = 0; index < components.Length; index++)
                {
                    BackupRestoreAclValidator.ValidateNamespaceParent(current, isWindows: true, isFilesystemAnchor: index == 0);

                    var access = FileListDirectory | FileTraverse | FileReadAttributes | FileReadControl | SynchronizeAccess;
                    if (index == components.Length - 1)
                    {
                        access |= FileAddFile | FileAddSubdirectory;
                    }

                    var child = OpenWindowsRelative(
                        current,
                        components[index],
                        access,
                        FileOpen,
                        FileOpenReparsePoint | FileSynchronousIoNonAlert);
                    try
                    {
                        EnsureWindowsDirectory(child, "Restore root path contains a reparse point or non-directory entry.");
                    }
                    catch
                    {
                        child.Dispose();
                        throw;
                    }

                    ancestors.Add(current);
                    current = child;
                }

                var result = new BackupRestoreDirectory(current, isWindows: true, ancestors.ToArray());
                current = null;
                ancestors.Clear();
                return result;
            }
            catch
            {
                current?.Dispose();
                foreach (var ancestor in ancestors)
                {
                    ancestor.Dispose();
                }

                throw;
            }
        }

        if (OperatingSystem.IsLinux())
        {
            var (handle, ancestors) = OpenLinuxDirectoryPath(fullPath);
            return new BackupRestoreDirectory(handle, isWindows: false, ancestors);
        }

        throw new PlatformNotSupportedException("Safe restore filesystem operations require Windows or Linux.");
    }

    /// <summary>
    /// Validates this opened directory against the restore write trust policy.
    /// </summary>
    public void ValidateTrustedWriteBoundary()
    {
        BackupRestoreAclValidator.Validate(_handle, _isWindows);
    }

    public void ValidateTrustedNamespaceParent()
    {
        BackupRestoreAclValidator.ValidateNamespaceParent(_handle, _isWindows);
    }

    public void ValidateTrustedFile(FileStream file)
    {
        ArgumentNullException.ThrowIfNull(file);
        BackupRestoreAclValidator.ValidateFile(file.SafeFileHandle, _isWindows);
    }

    public bool IsSameOrAncestorOf(BackupRestoreDirectory candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (_isWindows != candidate._isWindows)
        {
            throw new ArgumentException("Restore directory authorities must use the same platform.", nameof(candidate));
        }

        var identity = GetDirectoryIdentity(_handle, _isWindows);
        foreach (var ancestor in candidate._ancestorHandles)
        {
            if (identity == GetDirectoryIdentity(ancestor, _isWindows))
            {
                return true;
            }
        }

        if (identity == GetDirectoryIdentity(candidate._handle, candidate._isWindows))
        {
            return true;
        }

        return !_isWindows && IsPhysicalAncestorOf(candidate);
    }

    public bool IsSameDirectoryAs(BackupRestoreDirectory candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (_isWindows != candidate._isWindows)
        {
            throw new ArgumentException("Restore directory authorities must use the same platform.", nameof(candidate));
        }

        return GetDirectoryIdentity(_handle, _isWindows) == GetDirectoryIdentity(candidate._handle, candidate._isWindows);
    }

    public bool PhysicallyOverlaps(BackupRestoreDirectory candidate)
        => IsSameOrAncestorOf(candidate) || candidate.IsSameOrAncestorOf(this);

    public bool IsOnSameMountAs(BackupRestoreDirectory candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (_isWindows != candidate._isWindows)
        {
            throw new ArgumentException("Restore directory authorities must use the same platform.", nameof(candidate));
        }

        return _isWindows
            ? GetDirectoryIdentity(_handle, isWindows: true).Volume == GetDirectoryIdentity(candidate._handle, isWindows: true).Volume
            : GetDirectoryMountId(_handle) == GetDirectoryMountId(candidate._handle);
    }

    private bool IsPhysicalAncestorOf(BackupRestoreDirectory candidate)
    {
        var sourceMountId = GetDirectoryMountId(_handle);
        var sourceVisiblePath = ReadLinuxDirectoryPath(_handle);
        var source = GetLinuxPhysicalLocation();
        var target = candidate.GetLinuxPhysicalLocation();
        if (source.DeviceMajor == target.DeviceMajor
            && source.DeviceMinor == target.DeviceMinor
            && IsLinuxPathWithin(target.Path, source.Path))
        {
            return true;
        }

        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            if (!TryParseLinuxMountInfoLine(line, out var entry))
            {
                throw new IOException("Could not parse /proc/self/mountinfo during nested-mount verification.");
            }

            if (entry.MountId == sourceMountId
                || !IsLinuxPathWithin(entry.MountPoint, sourceVisiblePath))
            {
                continue;
            }

            if (entry.DeviceMajor == target.DeviceMajor
                && entry.DeviceMinor == target.DeviceMinor
                && IsLinuxPathWithin(target.Path, entry.Root))
            {
                return true;
            }
        }

        if (sourceMountId != GetDirectoryMountId(_handle)
            || !string.Equals(sourceVisiblePath, ReadLinuxDirectoryPath(_handle), StringComparison.Ordinal)
            || source != GetLinuxPhysicalLocation()
            || target != candidate.GetLinuxPhysicalLocation())
        {
            throw new IOException("A verified restore directory moved during nested-mount verification.");
        }

        return false;
    }

    private LinuxPhysicalLocation GetLinuxPhysicalLocation()
    {
        var entry = ReadLinuxMountInfo(GetDirectoryMountId(_handle));
        var visiblePath = ReadLinuxDirectoryPath(_handle);
        if (!IsLinuxPathWithin(visiblePath, entry.MountPoint))
        {
            throw new IOException("The opened restore path does not match its mount-table entry.");
        }

        var relativePath = Path.GetRelativePath(entry.MountPoint, visiblePath);
        var physicalPath = relativePath == "." ? entry.Root : Path.GetFullPath(Path.Combine(entry.Root, relativePath));
        if (!string.Equals(visiblePath, ReadLinuxDirectoryPath(_handle), StringComparison.Ordinal))
        {
            throw new IOException("The opened restore directory moved during mount-source verification.");
        }

        return new LinuxPhysicalLocation(entry.DeviceMajor, entry.DeviceMinor, physicalPath);
    }

    private static LinuxMountInfoEntry ReadLinuxMountInfo(ulong mountId)
    {
        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            if (TryParseLinuxMountInfoLine(line, out var entry) && entry.MountId == mountId)
            {
                return entry;
            }
        }

        throw new IOException("Could not map the opened restore mount to /proc/self/mountinfo.");
    }

    private static string ReadLinuxDirectoryPath(SafeFileHandle handle)
    {
        var buffer = new byte[4096];
        var descriptorPath = $"/proc/self/fd/{handle.DangerousGetHandle().ToInt32().ToString(CultureInfo.InvariantCulture)}";
        var length = ReadLink(descriptorPath, buffer, (nuint)buffer.Length).ToInt64();
        if (length < 0)
        {
            throw CreateLinuxException("Could not resolve the current path of the opened restore directory.");
        }

        if (length == buffer.Length)
        {
            throw new IOException("The opened restore directory path exceeds the mount-source verification limit.");
        }

        var path = Encoding.UTF8.GetString(buffer, 0, checked((int)length));
        if (!Path.IsPathFullyQualified(path) || path.EndsWith(" (deleted)", StringComparison.Ordinal))
        {
            throw new IOException("The opened restore directory has no stable path in the current mount namespace.");
        }

        return Path.GetFullPath(path);
    }

    internal static bool TryParseLinuxMountInfoLine(string line, out LinuxMountInfoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(line);
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var device = fields.Length > 2 ? fields[2].Split(':', 2) : Array.Empty<string>();
        if (fields.Length < 6
            || !ulong.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var mountId)
            || device.Length != 2
            || !uint.TryParse(device[0], NumberStyles.None, CultureInfo.InvariantCulture, out var deviceMajor)
            || !uint.TryParse(device[1], NumberStyles.None, CultureInfo.InvariantCulture, out var deviceMinor))
        {
            entry = default;
            return false;
        }

        entry = new LinuxMountInfoEntry(mountId, deviceMajor, deviceMinor, DecodeMountInfoPath(fields[3]), DecodeMountInfoPath(fields[4]));
        return true;
    }

    private static string DecodeMountInfoPath(string path)
        => path.Replace("\\040", " ", StringComparison.Ordinal)
            .Replace("\\011", "\t", StringComparison.Ordinal)
            .Replace("\\012", "\n", StringComparison.Ordinal)
            .Replace("\\134", "\\", StringComparison.Ordinal);

    private static bool IsLinuxPathWithin(string path, string root)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedRoot = Path.GetFullPath(root);
        if (string.Equals(normalizedPath, normalizedRoot, StringComparison.Ordinal))
        {
            return true;
        }

        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    /// <summary>
    /// Opens one child directory without following symbolic links or reparse points.
    /// </summary>
    /// <param name="component">One directory name, not a path.</param>
    /// <returns>The opened child directory.</returns>
    public BackupRestoreDirectory OpenDirectory(string component)
    {
        ValidateComponent(component);
        if (_isWindows)
        {
            var child = OpenWindowsRelative(_handle, component, FileListDirectory | FileAddFile | FileAddSubdirectory | FileTraverse | FileReadAttributes | FileReadControl | SynchronizeAccess, FileOpen, FileOpenReparsePoint | FileSynchronousIoNonAlert);
            try
            {
                EnsureWindowsDirectory(child, "Restore path contains a reparse point or non-directory entry.");
                return new BackupRestoreDirectory(child, isWindows: true);
            }
            catch
            {
                child.Dispose();
                throw;
            }
        }

        var descriptor = OpenAt(GetDescriptor(), component, LinuxOpenPath | LinuxOpenDirectory | LinuxOpenNoFollow | LinuxOpenCloseOnExec);
        if (descriptor < 0)
        {
            throw CreateLinuxException("Failed to open child directory without following links.");
        }

        return new BackupRestoreDirectory(new SafeFileHandle((IntPtr)descriptor, ownsHandle: true), isWindows: false);
    }

    /// <summary>
    /// Creates and opens one child directory relative to this directory.
    /// Existing entries are never adopted.
    /// </summary>
    /// <param name="component">One directory name, not a path.</param>
    /// <param name="unixMode">POSIX mode applied subject to the process umask.</param>
    /// <returns>The newly created directory.</returns>
    public BackupRestoreDirectory CreateDirectory(string component, uint unixMode = 0x1C0)
    {
        ValidateComponent(component);
        if (_isWindows)
        {
            var child = OpenWindowsRelative(
                _handle,
                component,
                FileListDirectory | FileAddFile | FileAddSubdirectory | FileTraverse | FileReadAttributes | FileReadControl | SynchronizeAccess,
                FileCreate,
                FileDirectoryFile | FileSynchronousIoNonAlert);
            try
            {
                EnsureWindowsDirectory(child, "New restore directory is not a real directory.");
                return new BackupRestoreDirectory(child, isWindows: true);
            }
            catch
            {
                child.Dispose();
                throw;
            }
        }

        if (MkdirAt(GetDescriptor(), component, unixMode) != 0)
        {
            throw CreateLinuxException("Failed to create child directory relative to its opened parent.");
        }

        return OpenDirectory(component);
    }

    /// <summary>
    /// Opens a child directory or creates it relative to this directory.
    /// </summary>
    /// <param name="component">One directory name, not a path.</param>
    /// <param name="unixMode">POSIX mode applied when creating the directory.</param>
    /// <returns>The opened child directory.</returns>
    public BackupRestoreDirectory OpenOrCreateDirectory(string component, uint unixMode = 0x1C0)
        => OpenOrCreateDirectory(component, out _, unixMode);

    /// <summary>
    /// Opens one child directory or creates it relative to this directory.
    /// </summary>
    /// <param name="component">One directory name, not a path.</param>
    /// <param name="created">Whether this call created the directory.</param>
    /// <param name="unixMode">POSIX mode applied when creating the directory.</param>
    /// <returns>The opened child directory.</returns>
    public BackupRestoreDirectory OpenOrCreateDirectory(string component, out bool created, uint unixMode = 0x1C0)
    {
        ValidateComponent(component);
        created = false;
        if (_isWindows)
        {
            SafeFileHandle child;
            try
            {
                child = OpenWindowsRelative(
                    _handle,
                    component,
                    FileListDirectory | FileAddFile | FileAddSubdirectory | FileTraverse | FileReadAttributes | FileReadControl | SynchronizeAccess,
                    FileCreate,
                    FileDirectoryFile | FileSynchronousIoNonAlert | FileWriteThrough);
                created = true;
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode is 80 or 183)
            {
                child = OpenWindowsRelative(
                    _handle,
                    component,
                    FileListDirectory | FileAddFile | FileAddSubdirectory | FileTraverse | FileReadAttributes | FileReadControl | SynchronizeAccess,
                    FileOpen,
                    FileDirectoryFile | FileOpenReparsePoint | FileSynchronousIoNonAlert);
            }

            try
            {
                EnsureWindowsDirectory(child, "Restore path contains a reparse point or non-directory entry.");
                return new BackupRestoreDirectory(child, isWindows: true);
            }
            catch
            {
                child.Dispose();
                throw;
            }
        }

        if (MkdirAt(GetDescriptor(), component, unixMode) == 0)
        {
            created = true;
            FlushDirectory();
        }
        else if (Marshal.GetLastPInvokeError() != 17)
        {
            throw CreateLinuxException("Failed to create or open child directory relative to its opened parent.");
        }

        return OpenDirectory(component);
    }

    /// <summary>
    /// Creates one new regular file relative to this directory without replacing any entry.
    /// </summary>
    /// <param name="component">One file name, not a path.</param>
    /// <param name="unixMode">POSIX mode applied subject to the process umask.</param>
    /// <returns>A writable stream for the new file.</returns>
    public FileStream CreateNewFile(string component, uint unixMode = 0x180)
    {
        ValidateComponent(component);
        SafeFileHandle file;
        if (_isWindows)
        {
            file = OpenWindowsRelative(
                _handle,
                component,
                0x00000002 | FileReadAttributes | SynchronizeAccess,
                FileCreate,
                FileNonDirectoryFile | FileSynchronousIoNonAlert | FileWriteThrough);
        }
        else
        {
            var descriptor = OpenAt(
                GetDescriptor(),
                component,
                LinuxOpenWriteOnly | LinuxOpenCreate | LinuxOpenExclusive | LinuxOpenCloseOnExec | LinuxOpenNoFollow | LinuxOpenNonBlock,
                unixMode);
            if (descriptor < 0)
            {
                throw CreateLinuxException("Failed to create a new restore file without replacing an entry.");
            }

            file = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
            try
            {
                FlushDirectory();
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        return new FileStream(file, FileAccess.Write, 4096, isAsync: false);
    }

    /// <summary>
    /// Opens one existing regular file relative to this directory without following links.
    /// </summary>
    /// <param name="component">One file name, not a path.</param>
    /// <param name="access">Requested stream access.</param>
    /// <param name="share">Windows sharing permissions for the opened file.</param>
    /// <returns>A stream backed by the validated file handle.</returns>
    public FileStream OpenExistingFile(string component, FileAccess access = FileAccess.Read, FileShare share = FileShare.Read | FileShare.Write)
    {
        ValidateComponent(component);
        var nativeAccess = access switch
        {
            FileAccess.Read => 0x00000001u,
            FileAccess.Write => 0x00000002u,
            FileAccess.ReadWrite => 0x00000003u,
            _ => throw new ArgumentOutOfRangeException(nameof(access))
        };
        SafeFileHandle file;
        if (_isWindows)
        {
            file = OpenWindowsRelative(
                _handle,
                component,
                nativeAccess | FileReadAttributes | FileReadControl | SynchronizeAccess,
                FileOpen,
                FileNonDirectoryFile | FileOpenReparsePoint | FileSynchronousIoNonAlert,
                (uint)share);
            try
            {
                var attributes = GetWindowsAttributes(file);
                if ((attributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0)
                {
                    throw new IOException("Restore file must be a regular file, not a directory or reparse point.");
                }
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        else
        {
            var flags = (access switch
            {
                FileAccess.Read => 0,
                FileAccess.Write => LinuxOpenWriteOnly,
                FileAccess.ReadWrite => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(access))
            }) | LinuxOpenCloseOnExec | LinuxOpenNoFollow | LinuxOpenNonBlock;
            var descriptor = OpenAt(GetDescriptor(), component, flags);
            if (descriptor < 0)
            {
                throw CreateLinuxException("Failed to open an existing restore file without following links.");
            }

            file = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
            try
            {
                var info = new LinuxStatx();
                if (Statx(descriptor, "", LinuxAtEmptyPath, LinuxStatxType, ref info) != 0)
                {
                    throw CreateLinuxException("Failed to inspect the opened restore file handle.");
                }

                if ((info.Mask & LinuxStatxType) == 0 || (info.Mode & LinuxFileTypeMask) != LinuxFileTypeRegular)
                {
                    throw new IOException("Restore entry must be a regular file.");
                }
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        return new FileStream(file, access, 4096, isAsync: false);
    }

    /// <summary>
    /// Opens or creates one regular file relative to this directory without following links.
    /// </summary>
    /// <param name="component">One file name, not a path.</param>
    /// <param name="access">Requested stream access.</param>
    /// <returns>A stream backed by the validated file handle.</returns>
    public FileStream OpenOrCreateFile(string component, FileAccess access = FileAccess.ReadWrite)
    {
        ValidateComponent(component);
        var nativeAccess = access switch
        {
            FileAccess.Read => 0x00000001u,
            FileAccess.Write => 0x00000002u,
            FileAccess.ReadWrite => 0x00000003u,
            _ => throw new ArgumentOutOfRangeException(nameof(access))
        };
        SafeFileHandle file;
        if (_isWindows)
        {
            file = OpenWindowsRelative(
                _handle,
                component,
                nativeAccess | FileReadAttributes | FileReadControl | SynchronizeAccess,
                FileOpenIf,
                FileNonDirectoryFile | FileOpenReparsePoint | FileSynchronousIoNonAlert);
            try
            {
                var attributes = GetWindowsAttributes(file);
                if ((attributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0)
                {
                    throw new IOException("Restore file must be a regular file, not a directory or reparse point.");
                }
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        else
        {
            var flags = (access switch
            {
                FileAccess.Read => 0,
                FileAccess.Write => LinuxOpenWriteOnly,
                FileAccess.ReadWrite => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(access))
            }) | LinuxOpenCreate | LinuxOpenCloseOnExec | LinuxOpenNoFollow | LinuxOpenNonBlock;
            var descriptor = OpenAt(GetDescriptor(), component, flags, 0x180);
            if (descriptor < 0)
            {
                throw CreateLinuxException("Failed to open or create a restore file without following links.");
            }

            file = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
            try
            {
                var info = new LinuxStatx();
                if (Statx(descriptor, "", LinuxAtEmptyPath, LinuxStatxType, ref info) != 0)
                {
                    throw CreateLinuxException("Failed to inspect the opened restore file handle.");
                }

                if ((info.Mask & LinuxStatxType) == 0 || (info.Mode & LinuxFileTypeMask) != LinuxFileTypeRegular)
                {
                    throw new IOException("Restore entry must be a regular file.");
                }
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        return new FileStream(file, access, 4096, isAsync: false);
    }

    /// <summary>
    /// Renames one entry to one name under an opened destination directory.
    /// </summary>
    /// <param name="sourceComponent">One source name, not a path.</param>
    /// <param name="destinationDirectory">An opened destination directory.</param>
    /// <param name="destinationComponent">One destination name, not a path.</param>
    /// <param name="replaceExisting">Whether to replace an existing destination entry.</param>
    /// <remarks>Symbolic-link and reparse-point sources are rejected on all supported platforms.</remarks>
    public void Rename(
        string sourceComponent,
        BackupRestoreDirectory destinationDirectory,
        string destinationComponent,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(destinationDirectory);
        ValidateComponent(sourceComponent);
        ValidateComponent(destinationComponent);
        if (_isWindows != destinationDirectory._isWindows)
        {
            throw new ArgumentException("Source and destination directories must use the same platform backend.", nameof(destinationDirectory));
        }

        if (_isWindows)
        {
            using var source = OpenWindowsRelative(
                _handle,
                sourceComponent,
                DeleteAccess | FileReadAttributes | SynchronizeAccess,
                FileOpen,
                FileOpenReparsePoint | FileSynchronousIoNonAlert | FileWriteThrough);
            EnsureWindowsNotReparsePoint(source, "Restore rename source cannot be a reparse point.");
            try
            {
                RenameWindows(source, destinationDirectory._handle, destinationComponent, replaceExisting);
            }
            catch (Win32Exception exception)
            {
                throw new IOException("Failed to rename a restore entry relative to opened directories.", exception);
            }

            destinationDirectory.FlushDirectory();
            return;
        }

        if ((GetLinuxEntryType(sourceComponent) & LinuxFileTypeMask) == LinuxFileTypeSymbolicLink)
        {
            throw new IOException("Restore rename source cannot be a symbolic link.");
        }

        var result = replaceExisting
            ? RenameAt(GetDescriptor(), sourceComponent, destinationDirectory.GetDescriptor(), destinationComponent)
            : RenameAt2(GetDescriptor(), sourceComponent, destinationDirectory.GetDescriptor(), destinationComponent, LinuxRenameNoReplace);
        if (result != 0)
        {
            throw CreateLinuxException("Failed to rename an entry relative to opened directories.");
        }

        FlushDirectory();
        if (!ReferenceEquals(this, destinationDirectory))
        {
            destinationDirectory.FlushDirectory();
        }
    }

    /// <summary>
    /// Deletes one regular file entry relative to this directory without following links.
    /// </summary>
    /// <param name="component">One file name, not a path.</param>
    public void DeleteFile(string component)
    {
        DeleteEntry(component, directory: false);
    }

    /// <summary>
    /// Deletes one regular file entry relative to this directory, ignoring a missing entry.
    /// </summary>
    /// <param name="component">One file name, not a path.</param>
    public void DeleteFileIfExists(string component)
    {
        try
        {
            DeleteFile(component);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
        }
    }

    /// <summary>
    /// Deletes one non-directory entry relative to this directory without following links.
    /// </summary>
    /// <param name="component">One file name, not a path.</param>
    public void DeleteNonDirectoryEntryIfExists(string component)
    {
        ValidateComponent(component);
        try
        {
            if (_isWindows)
            {
                using var entry = OpenWindowsRelative(
                    _handle,
                    component,
                    DeleteAccess | FileReadAttributes | SynchronizeAccess,
                    FileOpen,
                    FileOpenReparsePoint | FileSynchronousIoNonAlert | FileWriteThrough);
                if ((GetWindowsAttributes(entry) & FileAttributeDirectory) != 0)
                {
                    throw new IOException("Restore delete target is a directory.");
                }

                var disposition = new FileDispositionInfo { DeleteFile = 1 };
                if (!SetFileInformationByHandle(entry, FileDispositionInfoClass, ref disposition, (uint)Marshal.SizeOf<FileDispositionInfo>()))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to delete restore entry through its opened handle.");
                }

                FlushDirectory();
                return;
            }

            if (UnlinkAt(GetDescriptor(), component, 0) != 0)
            {
                throw CreateLinuxException("Failed to delete restore entry relative to its opened directory.");
            }

            FlushDirectory();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
        }
    }

    /// <summary>
    /// Deletes one empty directory entry relative to this directory without following links.
    /// </summary>
    /// <param name="component">One directory name, not a path.</param>
    public void DeleteDirectory(string component)
    {
        DeleteEntry(component, directory: true);
    }

    /// <summary>
    /// Deletes one empty child directory relative to this directory, ignoring a missing entry.
    /// </summary>
    /// <param name="component">One directory name, not a path.</param>
    public void DeleteDirectoryIfExists(string component)
    {
        try
        {
            DeleteDirectory(component);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
        }
        catch (IOException exception) when (exception.InnerException is Win32Exception { NativeErrorCode: 2 or 3 })
        {
        }
    }

    /// <summary>
    /// Determines whether a regular file exists relative to this directory without following links.
    /// </summary>
    /// <param name="component">One file name, not a path.</param>
    /// <returns>Whether a regular file exists.</returns>
    public bool FileExists(string component)
    {
        try
        {
            using var file = OpenExistingFile(component);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return false;
        }
        catch (IOException exception) when (exception.InnerException is Win32Exception { NativeErrorCode: 2 or 3 })
        {
            return false;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 5)
        {
            throw new IOException("Could not determine whether the restore file exists because access was denied.", exception);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 267)
        {
            return false;
        }
    }

    /// <summary>Closes the opened directory handle.</summary>
    public void Dispose()
    {
        _handle.Dispose();
        for (var index = _ancestorHandles.Length - 1; index >= 0; index--)
        {
            _ancestorHandles[index].Dispose();
        }
    }

    private void DeleteEntry(string component, bool directory)
    {
        ValidateComponent(component);
        if (_isWindows)
        {
            using var entry = OpenWindowsRelative(
                _handle,
                component,
                DeleteAccess | FileReadAttributes | SynchronizeAccess,
                FileOpen,
                FileOpenReparsePoint | FileSynchronousIoNonAlert | FileWriteThrough);
            EnsureWindowsNotReparsePoint(entry, "Restore delete target cannot be a reparse point.");
            var attributes = GetWindowsAttributes(entry);
            if (((attributes & FileAttributeDirectory) != 0) != directory)
            {
                throw new IOException(directory ? "Restore delete target is not a directory." : "Restore delete target is not a regular file.");
            }

            var disposition = new FileDispositionInfo { DeleteFile = 1 };
            if (!SetFileInformationByHandle(entry, FileDispositionInfoClass, ref disposition, (uint)Marshal.SizeOf<FileDispositionInfo>()))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to delete restore entry through its opened handle.");
            }

            FlushDirectory();
            return;
        }

        var entryType = GetLinuxEntryType(component);
        var expectedType = directory ? LinuxFileTypeDirectory : LinuxFileTypeRegular;
        if ((entryType & LinuxFileTypeMask) != expectedType)
        {
            throw new IOException(directory ? "Restore delete target is not a directory." : "Restore delete target is not a regular file.");
        }

        if (UnlinkAt(GetDescriptor(), component, directory ? LinuxAtRemovedir : 0) != 0)
        {
            throw CreateLinuxException("Failed to delete an entry relative to its opened directory.");
        }

        FlushDirectory();
    }

    private void FlushDirectory()
    {
        if (_isWindows)
        {
            // Mutating Windows handles carry FILE_WRITE_THROUGH; Linux needs an explicit parent-directory fsync.
            return;
        }

        var descriptor = OpenAt(GetDescriptor(), ".", LinuxOpenDirectory | LinuxOpenNoFollow | LinuxOpenCloseOnExec);
        if (descriptor < 0)
        {
            throw CreateLinuxException("Failed to open a restore directory for flushing its namespace changes.");
        }

        using var directory = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        if (Fsync(directory.DangerousGetHandle().ToInt32()) != 0)
        {
            throw CreateLinuxException("Failed to flush a restore directory namespace change to disk.");
        }
    }

    private static (SafeFileHandle Handle, SafeFileHandle[] Ancestors) OpenLinuxDirectoryPath(string fullPath)
    {
        var descriptor = Open("/", LinuxOpenPath | LinuxOpenDirectory | LinuxOpenNoFollow | LinuxOpenCloseOnExec);
        if (descriptor < 0)
        {
            throw CreateLinuxException("Failed to open filesystem root for restore path traversal.");
        }

        SafeFileHandle? current = new((IntPtr)descriptor, ownsHandle: true);
        var ancestors = new List<SafeFileHandle>();
        try
        {
            var isFilesystemAnchor = true;
            foreach (var component in fullPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                BackupRestoreAclValidator.ValidateNamespaceParent(current, isWindows: false, isFilesystemAnchor);
                isFilesystemAnchor = false;

                var childDescriptor = OpenAt(
                    current.DangerousGetHandle().ToInt32(),
                    component,
                    LinuxOpenPath | LinuxOpenDirectory | LinuxOpenNoFollow | LinuxOpenCloseOnExec);
                if (childDescriptor < 0)
                {
                    throw CreateLinuxException("Restore root path contains a missing, non-directory, or symbolic-link component.");
                }

                ancestors.Add(current);
                current = new SafeFileHandle((IntPtr)childDescriptor, ownsHandle: true);
            }

            var result = (current, ancestors.ToArray());
            current = null;
            ancestors.Clear();
            return result;
        }
        finally
        {
            current?.Dispose();
            for (var index = ancestors.Count - 1; index >= 0; index--)
            {
                ancestors[index].Dispose();
            }
        }
    }

    private ushort GetLinuxEntryType(string component)
    {
        var info = new LinuxStatx();
        if (Statx(GetDescriptor(), component, LinuxAtSymlinkNoFollow, LinuxStatxType, ref info) != 0)
        {
            throw CreateLinuxException("Failed to inspect restore entry type without following symbolic links.");
        }

        if ((info.Mask & LinuxStatxType) == 0)
        {
            throw new IOException("Restore filesystem did not provide the entry type.");
        }

        return info.Mode;
    }

    private static DirectoryIdentity GetDirectoryIdentity(SafeFileHandle handle, bool isWindows)
    {
        if (isWindows)
        {
            if (!GetFileInformationByHandleEx(handle, FileIdInfoClass, out FileIdInformation info, (uint)Marshal.SizeOf<FileIdInformation>()))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not identify an opened restore directory.");
            }

            if (info.FileIdHigh == 0 && info.FileIdLow == 0)
            {
                throw new IOException("The restore filesystem did not provide a stable directory identity.");
            }

            return new DirectoryIdentity(info.VolumeSerialNumber, info.FileIdHigh, info.FileIdLow);
        }

        var descriptor = handle.DangerousGetHandle().ToInt32();
        var stat = new LinuxStatx();
        if (Statx(descriptor, string.Empty, LinuxAtEmptyPath, LinuxStatxType | LinuxStatxInode, ref stat) != 0)
        {
            throw CreateLinuxException("Could not identify an opened restore directory.");
        }

        if ((stat.Mask & (LinuxStatxType | LinuxStatxInode)) != (LinuxStatxType | LinuxStatxInode)
            || (stat.Mode & LinuxFileTypeMask) != LinuxFileTypeDirectory
            || stat.Inode == 0)
        {
            throw new IOException("The restore filesystem did not provide a stable directory identity.");
        }

        // A bind mount gets a new mount ID but retains the source directory's device and inode.
        var device = ((ulong)stat.DeviceMajor << 32) | stat.DeviceMinor;
        return new DirectoryIdentity(device, 0, stat.Inode);
    }

    private static ulong GetDirectoryMountId(SafeFileHandle handle)
    {
        var stat = new LinuxStatx();
        if (Statx(handle.DangerousGetHandle().ToInt32(), string.Empty, LinuxAtEmptyPath, LinuxStatxType | LinuxStatxMountId, ref stat) != 0)
        {
            throw CreateLinuxException("Could not identify the mount containing a restore directory.");
        }

        if ((stat.Mask & (LinuxStatxType | LinuxStatxMountId)) != (LinuxStatxType | LinuxStatxMountId)
            || (stat.Mode & LinuxFileTypeMask) != LinuxFileTypeDirectory
            || stat.MountId == 0)
        {
            throw new IOException("The restore filesystem did not provide a stable mount identity.");
        }

        return stat.MountId;
    }

    private int GetDescriptor()
    {
        ObjectDisposedException.ThrowIf(_handle.IsClosed || _handle.IsInvalid, this);

        return _handle.DangerousGetHandle().ToInt32();
    }

    private static SafeFileHandle OpenWindowsRelative(SafeFileHandle parent, string component, uint access, uint disposition, uint options, uint shareAccess = FileShareRead | FileShareWrite)
    {
        var nameBuffer = Marshal.StringToHGlobalUni(component);
        var name = new UnicodeString
        {
            Length = checked((ushort)(component.Length * sizeof(char))),
            MaximumLength = checked((ushort)((component.Length * sizeof(char)) + sizeof(char))),
            Buffer = nameBuffer
        };
        var namePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        Marshal.StructureToPtr(name, namePointer, fDeleteOld: false);
        var attributes = new ObjectAttributes
        {
            Length = Marshal.SizeOf<ObjectAttributes>(),
            RootDirectory = parent.DangerousGetHandle(),
            ObjectName = namePointer,
            Attributes = ObjectCaseInsensitive
        };

        try
        {
            var status = NtCreateFile(
                out var nativeHandle,
                access,
                ref attributes,
                out _,
                IntPtr.Zero,
                0x00000080,
                shareAccess,
                disposition,
                options,
                IntPtr.Zero,
                0);
            if (status < 0)
            {
                throw new Win32Exception((int)RtlNtStatusToDosError(status), "Failed to perform a relative restore filesystem operation.");
            }

            return new SafeFileHandle(nativeHandle, ownsHandle: true);
        }
        finally
        {
            Marshal.FreeHGlobal(namePointer);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static void EnsureWindowsDirectory(SafeFileHandle handle, string message)
    {
        EnsureWindowsNotReparsePoint(handle, message);
        if ((GetWindowsAttributes(handle) & FileAttributeDirectory) == 0)
        {
            throw new IOException(message);
        }
    }

    private static void EnsureWindowsNotReparsePoint(SafeFileHandle handle, string message)
    {
        if ((GetWindowsAttributes(handle) & FileAttributeReparsePoint) != 0)
        {
            throw new IOException(message);
        }
    }

    private static uint GetWindowsAttributes(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandleEx(handle, FileAttributeTagInfoClass, out FileAttributeTagInfo info, (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to inspect opened restore filesystem entry.");
        }

        return info.FileAttributes;
    }

    private static void RenameWindows(SafeFileHandle source, SafeFileHandle destinationDirectory, string destinationComponent, bool replaceExisting)
    {
        var nameBytes = checked(destinationComponent.Length * sizeof(char));
        var fileNameOffset = Marshal.OffsetOf<FileRenameInfo>(nameof(FileRenameInfo.FileName)).ToInt32();
        var bufferSize = checked(fileNameOffset + nameBytes);
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            Marshal.Copy(new byte[bufferSize], 0, buffer, bufferSize);
            var info = new FileRenameInfo
            {
                Flags = replaceExisting ? 1u : 0u,
                RootDirectory = destinationDirectory.DangerousGetHandle(),
                FileNameLength = (uint)nameBytes,
                FileName = '\0'
            };
            Marshal.StructureToPtr(info, buffer, fDeleteOld: false);
            Marshal.Copy(destinationComponent.ToCharArray(), 0, IntPtr.Add(buffer, fileNameOffset), destinationComponent.Length);
            var ioStatusBlock = default(IoStatusBlock);
            var status = NtSetInformationFile(source, ref ioStatusBlock, buffer, (uint)bufferSize, FileRenameInformationClass);
            if (status < 0)
            {
                var error = unchecked((int)RtlNtStatusToDosError(status));
                throw new Win32Exception(error, $"Failed to rename restore entry relative to an opened directory (NTSTATUS 0x{status:X8}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ValidateComponent(string component)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);
        if (component is "." or ".."
            || component.AsSpan().IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0
            || component.Any(char.IsControl)
            || component.EndsWith(' ') || component.EndsWith('.'))
        {
            throw new ArgumentException("Restore filesystem operations accept one safe path component only.", nameof(component));
        }
    }

    private static Win32Exception CreateLinuxException(string message)
    {
        return new Win32Exception(Marshal.GetLastPInvokeError(), message);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInformation
    {
        public ulong VolumeSerialNumber;
        public ulong FileIdLow;
        public ulong FileIdHigh;
    }

    private readonly record struct DirectoryIdentity(ulong Volume, ulong High, ulong Low);

    internal readonly record struct LinuxMountInfoEntry(ulong MountId, uint DeviceMajor, uint DeviceMinor, string Root, string MountPoint);

    private readonly record struct LinuxPhysicalLocation(uint DeviceMajor, uint DeviceMinor, string Path);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileRenameInfo
    {
        public uint Flags;
        public IntPtr RootDirectory;
        public uint FileNameLength;
        [MarshalAs(UnmanagedType.U2)]
        public char FileName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public UIntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        public byte DeleteFile;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStatx
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(28)]
        public ushort Mode;

        [FieldOffset(32)]
        public ulong Inode;

        [FieldOffset(136)]
        public uint DeviceMajor;

        [FieldOffset(140)]
        public uint DeviceMinor;

        [FieldOffset(144)]
        public ulong MountId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int fileInformationClass, out FileAttributeTagInfo fileInformation, uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetFileInformationByHandleEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int fileInformationClass, out FileIdInformation fileInformation, uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int fileInformationClass, ref FileDispositionInfo fileInformation, uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetFileInformationByHandle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int fileInformationClass, IntPtr fileInformation, uint bufferSize);

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(out IntPtr fileHandle, uint desiredAccess, ref ObjectAttributes objectAttributes, out IoStatusBlock ioStatusBlock, IntPtr allocationSize, uint fileAttributes, uint shareAccess, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(SafeFileHandle fileHandle, ref IoStatusBlock ioStatusBlock, IntPtr fileInformation, uint length, int fileInformationClass);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "readlink", SetLastError = true)]
    private static extern nint ReadLink([MarshalAs(UnmanagedType.LPUTF8Str)] string path, [Out] byte[] buffer, nuint bufferSize);

    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAt(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode = 0);

    [DllImport("libc", EntryPoint = "mkdirat", SetLastError = true)]
    private static extern int MkdirAt(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    [DllImport("libc", EntryPoint = "renameat", SetLastError = true)]
    private static extern int RenameAt(int oldDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath, int newDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    private static extern int RenameAt2(int oldDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath, int newDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath, uint flags);

    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAt(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(int fileDescriptor);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, ref LinuxStatx buffer);
}
