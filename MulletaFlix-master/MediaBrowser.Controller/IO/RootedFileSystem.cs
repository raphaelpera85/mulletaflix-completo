using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MediaBrowser.Controller.IO;

/// <summary>
/// Performs file deletion relative to an opened directory root.
/// </summary>
public static class RootedFileSystem
{
    private const uint DeleteAccess = 0x00010000;
    private const uint FileListDirectory = 0x00000001;
    private const uint FileTraverse = 0x00000020;
    private const uint FileReadAttributes = 0x00000080;
    private const uint SynchronizeAccess = 0x00100000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint FileOpen = 1;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int FileAttributeTagInfoClass = 9;
    private const int FileDispositionInfoClass = 4;

    /// <summary>
    /// Deletes a file beneath an opened Windows root without following descendant links.
    /// The configured root itself may resolve through a symbolic link or junction.
    /// </summary>
    /// <param name="rootPath">Configured directory that defines the allowed tree.</param>
    /// <param name="filePath">Candidate file path.</param>
    /// <returns><c>true</c> if a file was deleted; otherwise, <c>false</c> if it is outside the root or missing.</returns>
    /// <exception cref="PlatformNotSupportedException">The current operating system has no verified safe implementation.</exception>
    public static bool TryDeleteFile(string rootPath, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!TryGetRelativePath(rootPath, filePath, out var components))
        {
            return false;
        }

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                // shortcut: non-Windows deletion lacks verified ancestry protection; enable after a race-safe OS-specific implementation exists.
                throw new PlatformNotSupportedException("Rooted file deletion is currently supported only on Windows.");
            }

            return DeleteWindows(rootPath, components);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return false;
        }
    }

    private static bool TryGetRelativePath(string rootPath, string filePath, out string[] components)
    {
        components = [];
        try
        {
            var fullRoot = Path.GetFullPath(rootPath);
            var fullFile = Path.GetFullPath(filePath);
            var relative = Path.GetRelativePath(fullRoot, fullFile);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var prefix = ".." + Path.DirectorySeparatorChar;
            if (Path.IsPathRooted(relative)
                || relative is "." or ".."
                || relative.StartsWith(prefix, comparison)
                || (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar
                    && relative.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison)))
            {
                return false;
            }

            components = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (components.Length == 0 || components.Any(IsInvalidComponent))
            {
                components = [];
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static bool IsInvalidComponent(string component)
    {
        if (component is "." or ".." || component.Contains('\0', StringComparison.Ordinal) || component.Any(char.IsControl))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            return component.Contains('/', StringComparison.Ordinal);
        }

        return component.IndexOfAny(['/', '\\', ':']) >= 0
            || component.EndsWith(' ')
            || component.EndsWith('.')
            || FileSystemHelper.IsWindowsDeviceName(component);
    }

    private static bool DeleteWindows(string rootPath, string[] components)
    {
        using var root = CreateFileW(
            Path.GetFullPath(rootPath),
            FileListDirectory | FileTraverse | FileReadAttributes | SynchronizeAccess,
            FileShareReadWrite,
            IntPtr.Zero,
            3,
            0x02000000,
            IntPtr.Zero);
        EnsureHandle(root, "Failed to open configured root directory.");
        if (!EnsureDirectory(root))
        {
            return false;
        }

        var parent = root;
        var openedDirectories = new List<SafeFileHandle>();
        try
        {
            // Omitting FILE_SHARE_DELETE on opened directories blocks renames while the handles are held.
            foreach (var component in components[..^1])
            {
                var child = OpenWindowsRelative(
                    parent,
                    component,
                    FileListDirectory | FileTraverse | FileReadAttributes | SynchronizeAccess,
                    FileOpen,
                    FileOpenReparsePoint | FileSynchronousIoNonAlert | FileDirectoryFile);
                try
                {
                    if (!EnsureDirectory(child))
                    {
                        child.Dispose();
                        return false;
                    }
                }
                catch
                {
                    child.Dispose();
                    throw;
                }

                openedDirectories.Add(child);
                parent = child;
            }

            using var file = OpenWindowsRelative(
                parent,
                components[^1],
                DeleteAccess | FileReadAttributes | SynchronizeAccess,
                FileOpen,
                FileOpenReparsePoint | FileSynchronousIoNonAlert | FileNonDirectoryFile);
            var disposition = new FileDispositionInfo { DeleteFile = 1 };
            if (!SetFileInformationByHandle(file, FileDispositionInfoClass, ref disposition, (uint)Marshal.SizeOf<FileDispositionInfo>()))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to delete file through its opened handle.");
            }

            return true;
        }
        finally
        {
            foreach (var directory in openedDirectories)
            {
                directory.Dispose();
            }
        }
    }

    private static SafeFileHandle OpenWindowsRelative(SafeFileHandle parent, string component, uint access, uint disposition, uint options)
    {
        var nameBuffer = Marshal.StringToHGlobalUni(component);
        var name = new UnicodeString
        {
            Length = checked((ushort)(component.Length * sizeof(char))),
            MaximumLength = checked((ushort)(component.Length * sizeof(char) + sizeof(char))),
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
                FileShareReadWrite,
                disposition,
                options,
                IntPtr.Zero,
                0);
            if (status < 0)
            {
                throw new Win32Exception((int)RtlNtStatusToDosError(status), "Failed to open a path component relative to its parent handle.");
            }

            return new SafeFileHandle(nativeHandle, ownsHandle: true);
        }
        finally
        {
            Marshal.FreeHGlobal(namePointer);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static void EnsureHandle(SafeFileHandle handle, string message)
    {
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(error, message);
        }
    }

    private static bool EnsureDirectory(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandleEx(handle, FileAttributeTagInfoClass, out var info, (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to inspect opened directory.");
        }

        if ((info.FileAttributes & FileAttributeDirectory) == 0 || (info.FileAttributes & FileAttributeReparsePoint) != 0)
        {
            return false;
        }

        return true;
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
    private struct FileDispositionInfo
    {
        public byte DeleteFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public UIntPtr Information;
    }

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CreateFileW", CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetFileInformationByHandleEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int fileInformationClass, out FileAttributeTagInfo fileInformation, uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetFileInformationByHandle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int fileInformationClass, ref FileDispositionInfo fileInformation, uint bufferSize);

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(out IntPtr fileHandle, uint desiredAccess, ref ObjectAttributes objectAttributes, out IoStatusBlock ioStatusBlock, IntPtr allocationSize, uint fileAttributes, uint shareAccess, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);

}
