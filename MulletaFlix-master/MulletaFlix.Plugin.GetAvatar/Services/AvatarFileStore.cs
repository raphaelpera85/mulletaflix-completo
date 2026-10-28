using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MulletaFlix.Plugin.GetAvatar.Services;

/// <summary>
/// Performs avatar and profile image file operations relative to an opened directory handle.
/// </summary>
public sealed class AvatarFileStore : IDisposable
{
    private const uint NtStatusObjectNameNotFound = 0xC0000034;
    private const uint NtStatusObjectNameCollision = 0xC0000035;
    private const uint NtStatusObjectPathNotFound = 0xC000003A;
    private const uint NtStatusReparsePointEncountered = 0xC000050B;

    private const uint FileReadData = 0x0001;
    private const uint FileWriteData = 0x0002;
    private const uint FileReadAttributes = 0x0080;
    private const uint DeleteAccess = 0x00010000;
    private const uint SynchronizeAccess = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint FileOpen = 1;
    private const uint FileCreate = 2;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int FileDispositionInfoClass = 4;

    private const int AtCurrentWorkingDirectory = -100;
    private const int AtEmptyPath = 0x1000;
    private const int AtRemovedir = 0x0200;
    private const uint StatxType = 0x0001;
    private const uint StatxMode = 0x0002;
    private const uint StatxBasicStats = 0x07FF;
    private const uint StatxUserId = 0x0008;
    private const uint StatxInode = 0x0100;
    private const ushort LinuxFileTypeMask = 0xF000;
    private const ushort LinuxRegularFile = 0x8000;
    private const ushort LinuxDirectory = 0x4000;
    private const ushort LinuxGroupWrite = 0x0010;
    private const ushort LinuxOtherWrite = 0x0002;
    private const ulong LinuxOpenReadOnly = 0;
    private const ulong LinuxOpenWriteOnly = 1;
    private const ulong LinuxOpenCreate = 0x40;
    private const ulong LinuxOpenExclusive = 0x80;
    private const ulong LinuxOpenNonBlock = 0x800;
    private const ulong LinuxOpenDirectory = 0x10000;
    private const ulong LinuxOpenNoFollow = 0x20000;
    private const ulong LinuxOpenCloseOnExec = 0x80000;
    private const ulong LinuxOpenPath = 0x200000;
    private const uint LinuxCreateMode = 0x180; // 0600
    private const int LinuxErrNoEntry = 2;
    private const int LinuxErrExists = 17;
    private const int LinuxErrNotDirectory = 20;
    private const int LinuxErrTooManyLinks = 40;
    private const uint LinuxRequiredIdentityMask = StatxType | StatxUserId | StatxInode;
    private const uint LinuxRequiredDirectoryMask = LinuxRequiredIdentityMask | StatxMode;

    private readonly SafeFileHandle _rootHandle;
    private readonly bool _isWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarFileStore"/> class.
    /// </summary>
    /// <param name="rootDirectory">The trusted avatar or profile image directory.</param>
    public AvatarFileStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var fullPath = Path.GetFullPath(rootDirectory);

        if (OperatingSystem.IsWindows())
        {
            _isWindows = true;
            _rootHandle = OpenWindowsRoot(fullPath);
        }
        else if (OperatingSystem.IsLinux())
        {
            _rootHandle = OpenLinuxRoot(fullPath);
        }
        else
        {
            throw new PlatformNotSupportedException("GetAvatar file operations require Windows or Linux handle-relative I/O.");
        }
    }

    /// <summary>
    /// Opens a regular avatar file relative to the pinned directory.
    /// </summary>
    /// <param name="fileName">A single file name.</param>
    /// <returns>The opened stream, or <see langword="null"/> when the entry is missing or unsafe.</returns>
    public FileStream? TryOpenRead(string fileName)
    {
        if (!IsSimpleFileName(fileName))
        {
            return null;
        }

        if (!_isWindows)
        {
            EnsureLinuxRootIsPrivate();
        }

        var handle = _isWindows ? TryOpenWindowsFile(fileName, FileReadData | FileReadAttributes | SynchronizeAccess, FileOpen, out _)
            : TryOpenLinuxRegularFileRead(fileName);
        if (handle is null)
        {
            return null;
        }

        if (!IsRegularFile(handle))
        {
            handle.Dispose();
            return null;
        }

        return new FileStream(handle, FileAccess.Read, 4096, isAsync: false);
    }

    /// <summary>
    /// Creates a new regular file without following links or overwriting an existing entry.
    /// </summary>
    /// <param name="fileName">A single file name.</param>
    /// <returns>The opened stream.</returns>
    public FileStream CreateNew(string fileName)
    {
        if (!IsSimpleFileName(fileName))
        {
            throw new ArgumentException("Avatar file name must be a single path component.", nameof(fileName));
        }

        if (!_isWindows)
        {
            EnsureLinuxRootIsPrivate();
        }

        var handle = _isWindows ? OpenWindowsFile(fileName, FileWriteData | FileReadAttributes | SynchronizeAccess, FileCreate)
            : OpenLinuxFile(fileName, LinuxOpenWriteOnly | LinuxOpenCreate | LinuxOpenExclusive | LinuxOpenCloseOnExec | LinuxOpenNoFollow | LinuxOpenNonBlock, LinuxCreateMode);
        return new FileStream(handle, FileAccess.Write, 4096, isAsync: false);
    }

    /// <summary>
    /// Deletes a file relative to the pinned directory without following links.
    /// </summary>
    /// <param name="fileName">A single file name.</param>
    /// <param name="beforeDelete">An optional persistence action invoked after validation and before deletion.</param>
    /// <returns>The result of the delete attempt.</returns>
    public AvatarFileDeleteResult TryDelete(string fileName, Action? beforeDelete = null)
    {
        if (!IsSimpleFileName(fileName))
        {
            return AvatarFileDeleteResult.Unsafe;
        }

        if (!_isWindows)
        {
            EnsureLinuxRootIsPrivate();
        }

        if (_isWindows)
        {
            using var handle = TryOpenWindowsFile(fileName, DeleteAccess | FileReadAttributes | SynchronizeAccess, FileOpen, out var unsafeEntry);
            if (unsafeEntry)
            {
                return AvatarFileDeleteResult.Unsafe;
            }

            if (handle is null)
            {
                beforeDelete?.Invoke();
                return AvatarFileDeleteResult.NotFound;
            }

            if (!IsRegularFile(handle))
            {
                return AvatarFileDeleteResult.Unsafe;
            }

            beforeDelete?.Invoke();
            var disposition = new FileDispositionInfo { DeleteFile = 1 };
            if (!SetFileInformationByHandle(handle, FileDispositionInfoClass, ref disposition, (uint)Marshal.SizeOf<FileDispositionInfo>()))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to delete avatar through its opened file handle.");
            }

            return AvatarFileDeleteResult.Deleted;
        }

        using var entry = TryOpenLinuxFile(fileName, LinuxOpenPath | LinuxOpenCloseOnExec | LinuxOpenNoFollow, 0, out var unsafeLinuxEntry);
        if (unsafeLinuxEntry)
        {
            return AvatarFileDeleteResult.Unsafe;
        }

        if (entry is null)
        {
            beforeDelete?.Invoke();
            return AvatarFileDeleteResult.NotFound;
        }

        if (!IsRegularFile(entry))
        {
            return AvatarFileDeleteResult.Unsafe;
        }

        if (!TryGetLinuxIdentity(entry, out var openedIdentity))
        {
            return AvatarFileDeleteResult.Unsafe;
        }

        beforeDelete?.Invoke();

        using var currentEntry = TryOpenLinuxFile(fileName, LinuxOpenPath | LinuxOpenCloseOnExec | LinuxOpenNoFollow, 0, out var unsafeCurrentEntry);
        if (unsafeCurrentEntry || currentEntry is null
            || !IsRegularFile(currentEntry)
            || !TryGetLinuxIdentity(currentEntry, out var currentIdentity)
            || currentIdentity != openedIdentity)
        {
            return AvatarFileDeleteResult.Unsafe;
        }

        if (UnlinkAt(_rootHandle, fileName, 0) == 0)
        {
            return AvatarFileDeleteResult.Deleted;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error == LinuxErrNoEntry)
        {
            return AvatarFileDeleteResult.NotFound;
        }

        if (error == LinuxErrNotDirectory || error == LinuxErrTooManyLinks)
        {
            return AvatarFileDeleteResult.Unsafe;
        }

        throw new Win32Exception(error, "Failed to unlink avatar entry relative to its opened directory.");
    }

    /// <inheritdoc />
    public void Dispose() => _rootHandle.Dispose();

    private static bool IsSimpleFileName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName is not "." and not ".."
            && fileName.AsSpan().IndexOfAny(new[] { '/', '\\', ':', '\0' }) < 0;
    }

    private SafeFileHandle? TryOpenWindowsFile(string fileName, uint access, uint disposition, out bool unsafeEntry)
    {
        unsafeEntry = false;
        var rootHandleAddedRef = false;
        _rootHandle.DangerousAddRef(ref rootHandleAddedRef);
        int status;
        IntPtr handle;
        try
        {
            status = NtCreateRelativeFile(fileName, access, FileShareRead | FileShareDelete, disposition, FileNonDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint, out handle);
        }
        finally
        {
            if (rootHandleAddedRef)
            {
                _rootHandle.DangerousRelease();
            }
        }
        if (status >= 0)
        {
            return new SafeFileHandle(handle, ownsHandle: true);
        }

        if (unchecked((uint)status) == NtStatusReparsePointEncountered)
        {
            unsafeEntry = true;
            return null;
        }

        if (unchecked((uint)status) is NtStatusObjectNameNotFound or NtStatusObjectNameCollision or NtStatusObjectPathNotFound)
        {
            return null;
        }

        var win32Error = RtlNtStatusToDosError(status);
        throw new Win32Exception((int)win32Error, "Failed to open avatar relative to its pinned directory.");
    }

    private SafeFileHandle OpenWindowsFile(string fileName, uint access, uint disposition)
    {
        return TryOpenWindowsFile(fileName, access, disposition, out _)
            ?? throw new IOException($"Avatar file '{fileName}' already exists or cannot be opened safely.");
    }

    private int NtCreateRelativeFile(string fileName, uint access, uint shareAccess, uint disposition, uint options, out IntPtr handle)
    {
        var nameBuffer = Marshal.StringToHGlobalUni(fileName);
        var unicodeName = new UnicodeString
        {
            Length = checked((ushort)(fileName.Length * sizeof(char))),
            MaximumLength = checked((ushort)((fileName.Length * sizeof(char)) + sizeof(char))),
            Buffer = nameBuffer
        };

        var unicodeNamePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        Marshal.StructureToPtr(unicodeName, unicodeNamePointer, fDeleteOld: false);
        var objectAttributes = new ObjectAttributes
        {
            Length = Marshal.SizeOf<ObjectAttributes>(),
            RootDirectory = _rootHandle.DangerousGetHandle(),
            ObjectName = unicodeNamePointer,
            Attributes = ObjectCaseInsensitive | ObjectDontReparse
        };

        try
        {
            return NtCreateFile(
                out handle,
                access,
                ref objectAttributes,
                out _,
                IntPtr.Zero,
                0,
                shareAccess,
                disposition,
                options,
                IntPtr.Zero,
                0);
        }
        finally
        {
            Marshal.FreeHGlobal(unicodeNamePointer);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static SafeFileHandle OpenWindowsRoot(string rootDirectory)
    {
        var handle = CreateFileW(rootDirectory, 0x0080 | 0x0001 | SynchronizeAccess, FileShareRead | FileShareWrite, IntPtr.Zero, 3, 0x02000000 | FileOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to open the GetAvatar catalog directory.");
        }

        if (!GetFileInformationByHandle(handle, out var info)
            || (info.FileAttributes & FileAttributeDirectory) == 0
            || (info.FileAttributes & FileAttributeReparsePoint) != 0)
        {
            handle.Dispose();
            throw new IOException("GetAvatar file root must be a real directory, not a reparse point.");
        }

        return handle;
    }

    private SafeFileHandle? TryOpenLinuxFile(string fileName, ulong flags, uint mode, out bool unsafeEntry)
    {
        unsafeEntry = false;
        var rootHandleAddedRef = false;
        _rootHandle.DangerousAddRef(ref rootHandleAddedRef);
        nint fd;
        try
        {
            fd = LinuxOpenAt(checked((int)_rootHandle.DangerousGetHandle()), fileName, checked((int)flags), mode);
        }
        finally
        {
            if (rootHandleAddedRef)
            {
                _rootHandle.DangerousRelease();
            }
        }
        if (fd >= 0)
        {
            return new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        }

        var error = Marshal.GetLastPInvokeError();
        if (error == LinuxErrTooManyLinks)
        {
            unsafeEntry = true;
            return null;
        }

        if (error is LinuxErrNoEntry or LinuxErrExists or LinuxErrNotDirectory)
        {
            return null;
        }

        throw new Win32Exception(error, "openat failed to open avatar relative to its pinned directory.");
    }

    private SafeFileHandle OpenLinuxFile(string fileName, ulong flags, uint mode)
    {
        return TryOpenLinuxFile(fileName, flags, mode, out _)
            ?? throw new IOException($"Avatar file '{fileName}' already exists or cannot be opened safely.");
    }

    private static SafeFileHandle OpenLinuxRoot(string rootDirectory)
    {
        var fd = LinuxOpenAt(AtCurrentWorkingDirectory, rootDirectory, checked((int)(LinuxOpenPath | LinuxOpenDirectory | LinuxOpenCloseOnExec | LinuxOpenNoFollow)), 0);
        if (fd < 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "openat could not pin the GetAvatar catalog directory without following links.");
        }

        var handle = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        if (!TryGetLinuxStatx(handle, StatxBasicStats, out var statx)
            || (statx.Mask & LinuxRequiredDirectoryMask) != LinuxRequiredDirectoryMask
            || (statx.Mode & LinuxFileTypeMask) != LinuxDirectory
            || statx.UserId != LinuxGetEffectiveUserId()
            || (statx.Mode & (LinuxGroupWrite | LinuxOtherWrite)) != 0)
        {
            handle.Dispose();
            throw new IOException("GetAvatar file root must be a real directory owned by the server user and not writable by other users.");
        }

        return handle;
    }

    private bool IsRegularFile(SafeFileHandle handle)
    {
        if (_isWindows)
        {
            return GetFileInformationByHandle(handle, out var info)
                && (info.FileAttributes & (FileAttributeDirectory | FileAttributeReparsePoint)) == 0;
        }

        return GetLinuxFileType(handle, out var mode) && (mode & LinuxFileTypeMask) == LinuxRegularFile;
    }

    private static bool GetLinuxFileType(SafeFileHandle handle, out ushort mode)
    {
        if (!TryGetLinuxStatx(handle, StatxType, out var statx))
        {
            mode = 0;
            return false;
        }

        mode = statx.Mode;
        return true;
    }

    private SafeFileHandle? TryOpenLinuxRegularFileRead(string fileName)
    {
        using var pathHandle = TryOpenLinuxFile(fileName, LinuxOpenPath | LinuxOpenCloseOnExec | LinuxOpenNoFollow, 0, out _);
        if (pathHandle is null || !IsRegularFile(pathHandle) || !TryGetLinuxIdentity(pathHandle, out var pathIdentity))
        {
            return null;
        }

        // O_PATH lets us verify the inode type before opening it for data access.
        // Opening the procfs magic link keeps the subsequent read attached to that inode,
        // even if its directory entry is renamed after validation.
        var procPath = $"/proc/self/fd/{pathHandle.DangerousGetHandle().ToInt32()}";
        var readFd = LinuxOpenAt(AtCurrentWorkingDirectory, procPath, checked((int)(LinuxOpenReadOnly | LinuxOpenCloseOnExec | LinuxOpenNonBlock)), 0);
        if (readFd < 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not open the verified avatar inode through procfs.");
        }

        var readHandle = new SafeFileHandle((IntPtr)readFd, ownsHandle: true);
        if (!IsRegularFile(readHandle)
            || !TryGetLinuxIdentity(readHandle, out var readIdentity)
            || readIdentity != pathIdentity)
        {
            readHandle.Dispose();
            return null;
        }

        return readHandle;
    }

    private void EnsureLinuxRootIsPrivate()
    {
        if (!TryGetLinuxStatx(_rootHandle, StatxBasicStats, out var statx)
            || (statx.Mask & LinuxRequiredDirectoryMask) != LinuxRequiredDirectoryMask
            || (statx.Mode & LinuxFileTypeMask) != LinuxDirectory
            || statx.UserId != LinuxGetEffectiveUserId()
            || (statx.Mode & (LinuxGroupWrite | LinuxOtherWrite)) != 0)
        {
            throw new IOException("GetAvatar file root ownership or permissions changed; refusing Linux file operation.");
        }
    }

    private static bool TryGetLinuxIdentity(SafeFileHandle handle, out LinuxFileIdentity identity)
    {
        if (!TryGetLinuxStatx(handle, StatxBasicStats, out var statx)
            || (statx.Mask & LinuxRequiredIdentityMask) != LinuxRequiredIdentityMask)
        {
            identity = default;
            return false;
        }

        identity = new LinuxFileIdentity(statx.DeviceMajor, statx.DeviceMinor, statx.Inode);
        return true;
    }

    private static bool TryGetLinuxStatx(SafeFileHandle handle, uint mask, out LinuxStatx statx)
    {
        statx = default;
        return LinuxStatxCall(handle.DangerousGetHandle().ToInt32(), string.Empty, AtEmptyPath, mask, ref statx) == 0;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int fileInformationClass, ref FileDispositionInfo information, uint bufferSize);

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(out IntPtr fileHandle, uint desiredAccess, ref ObjectAttributes objectAttributes, out IoStatusBlock ioStatusBlock, IntPtr allocationSize, uint fileAttributes, uint shareAccess, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);

    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern nint LinuxOpenAt(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int LinuxStatxCall(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, ref LinuxStatx buffer);

    [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
    private static extern uint LinuxGetEffectiveUserId();

    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAt(SafeFileHandle dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

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
    private struct IoStatusBlock
    {
        public IntPtr StatusOrPointer;
        public UIntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        public byte DeleteFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStatx
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(20)]
        public uint UserId;

        [FieldOffset(28)]
        public ushort Mode;

        [FieldOffset(32)]
        public ulong Inode;

        [FieldOffset(136)]
        public uint DeviceMajor;

        [FieldOffset(140)]
        public uint DeviceMinor;
    }

    private readonly record struct LinuxFileIdentity(uint DeviceMajor, uint DeviceMinor, ulong Inode);
}
