using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MulletaFlix.Server.Implementations.FullSystemBackup;

/// <summary>
/// Enforces the restore filesystem trust policy against opened directory handles.
/// </summary>
internal static class BackupRestoreAclValidator
{
    private const uint OwnerSecurityInformation = 0x00000001;
    private const uint DaclSecurityInformation = 0x00000004;
    private const uint FileObjectSecurityType = 1;
    private const uint FileWriteData = 0x00000002;
    // On a directory, FILE_APPEND_DATA's bit is FILE_ADD_SUBDIRECTORY.
    private const uint FileAppendData = 0x00000004;
    private const uint FileWriteExtendedAttributes = 0x00000010;
    private const uint FileDeleteChild = 0x00000040;
    private const uint FileWriteAttributes = 0x00000100;
    private const uint Delete = 0x00010000;
    private const uint WriteDac = 0x00040000;
    private const uint WriteOwner = 0x00080000;
    private const uint GenericWrite = 0x40000000;
    private const uint GenericAll = 0x10000000;
    private const uint WindowsWriteRights =
        FileWriteData
        | FileAppendData
        | FileWriteExtendedAttributes
        | FileDeleteChild
        | FileWriteAttributes
        | Delete
        | WriteDac
        | WriteOwner
        | GenericWrite
        | GenericAll;

    private const uint WindowsNamespaceParentWriteRights = WindowsWriteRights & ~FileAppendData;
    private const int LinuxAtEmptyPath = 0x1000;
    private const uint LinuxStatxBasicStats = 0x000007ff;
    private const ushort LinuxAclUserObject = 0x0001;
    private const ushort LinuxAclUser = 0x0002;
    private const ushort LinuxAclGroupObject = 0x0004;
    private const ushort LinuxAclGroup = 0x0008;
    private const ushort LinuxAclMask = 0x0010;
    private const ushort LinuxAclOther = 0x0020;
    private const ushort LinuxAclWrite = 0x0002;
    private const uint LinuxAclUndefinedId = uint.MaxValue;
    private const int LinuxErrorNoData = 61;
    private const int LinuxErrorNotSupported = 95;
    private const int LinuxOpenDirectory = 0x10000;
    private const int LinuxOpenCloseOnExec = 0x80000;
    private const uint WindowsDriveTypeFixed = 3;
    private const string WindowsTrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    private const long LinuxExtFilesystem = 0x0000EF53;
    private const long LinuxXfsFilesystem = 0x58465342;
    private const long LinuxBtrfsFilesystem = 0x9123683E;
    private const long LinuxTmpfsFilesystem = 0x01021994;
    private const long LinuxF2fsFilesystem = 0xF2F52010;
    private const long LinuxZfsFilesystem = 0x2FC12FC1;
    private const long LinuxReiserFilesystem = 0x52654973;
    private const long LinuxJfsFilesystem = 0x3153464A;
    private const long LinuxBcachefsFilesystem = 0xCA451A4E;
    private const long LinuxNtfsFilesystem = 0x5346544E;
    private const long LinuxExfatFilesystem = 0x2011BAB0;

    private static readonly HashSet<long> LocalLinuxFilesystems =
    [
        LinuxExtFilesystem,
        LinuxXfsFilesystem,
        LinuxBtrfsFilesystem,
        LinuxTmpfsFilesystem,
        LinuxF2fsFilesystem,
        LinuxZfsFilesystem,
        LinuxReiserFilesystem,
        LinuxJfsFilesystem,
        LinuxBcachefsFilesystem,
        LinuxNtfsFilesystem,
        LinuxExfatFilesystem
    ];

    internal static bool IsLocalLinuxFilesystem(long filesystemType)
        => LocalLinuxFilesystems.Contains(filesystemType);

    internal static bool IsTrustedWindowsFilesystem(string filesystemName)
        => string.Equals(filesystemName, "NTFS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(filesystemName, "ReFS", StringComparison.OrdinalIgnoreCase);

    public static void Validate(SafeFileHandle rootHandle, bool isWindows)
    {
        if (isWindows)
        {
            var trustedSids = GetTrustedWindowsSids();
            ValidateWindowsDirectory(rootHandle, trustedSids, isFinalDirectory: true, isNamespaceParent: false);
            return;
        }

        var effectiveUserId = GetEffectiveUserId();
        ValidateLinuxDirectory(rootHandle, effectiveUserId);
    }

    public static void ValidateNamespaceParent(SafeFileHandle directoryHandle, bool isWindows, bool isFilesystemAnchor = false)
    {
        if (isWindows)
        {
            var trustedSids = GetTrustedWindowsSids();
            ValidateWindowsDirectory(directoryHandle, trustedSids, isFinalDirectory: false, isNamespaceParent: true, isFilesystemAnchor: isFilesystemAnchor);
            return;
        }

        ValidateLinuxDirectory(directoryHandle, GetEffectiveUserId());
    }

    public static void ValidateFile(SafeFileHandle fileHandle, bool isWindows)
    {
        if (isWindows)
        {
            ValidateWindowsDirectory(fileHandle, GetTrustedWindowsSids(), isFinalDirectory: false, isNamespaceParent: false);
            return;
        }

        var effectiveUserId = GetEffectiveUserId();
        var info = new LinuxStatx();
        const uint requiredStatxMask = 0x0000000B;
        if (Statx(fileHandle, "", LinuxAtEmptyPath, LinuxStatxBasicStats, ref info) != 0
            || (info.Mask & requiredStatxMask) != requiredStatxMask
            || (info.Mode & 0xF000) != 0x8000)
        {
            throw new IOException("Could not read complete owner, mode, and type metadata for a restore file handle.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        if (info.Uid != effectiveUserId && info.Uid != 0)
        {
            throw new UnauthorizedAccessException("The restore file owner is not the service or root.");
        }

        if ((info.Mode & 0x0012) != 0)
        {
            throw new UnauthorizedAccessException("The restore file grants group or other write access.");
        }

        ValidateLinuxAcl(fileHandle, "system.posix_acl_access", effectiveUserId);
    }

    private static HashSet<string> GetTrustedWindowsSids()
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0008, out var token))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not open the restore service process token.");
        }

        using (token)
        {
            _ = GetTokenInformation(token, 1, IntPtr.Zero, 0, out var bufferLength);
            var error = Marshal.GetLastPInvokeError();
            if (bufferLength == 0 || error != 122)
            {
                throw new Win32Exception(error, "Could not size the restore service token identity.");
            }

            var tokenBuffer = Marshal.AllocHGlobal((int)bufferLength);
            try
            {
                if (!GetTokenInformation(token, 1, tokenBuffer, bufferLength, out _))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not read the restore service token identity.");
                }

                var currentUser = GetSidString(Marshal.ReadIntPtr(tokenBuffer));
                return
                [
                    currentUser,
                    "S-1-5-18",
                    "S-1-5-32-544"
                ];
            }
            finally
            {
                Marshal.FreeHGlobal(tokenBuffer);
            }
        }
    }

    private static void ValidateWindowsDirectory(
        SafeFileHandle handle,
        HashSet<string> trustedSids,
        bool isFinalDirectory,
        bool isNamespaceParent,
        bool isFilesystemAnchor = false)
    {
        var finalPath = GetFinalPath(handle);
        if (finalPath.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)
            || finalPath.StartsWith("\\Device\\Mup\\", StringComparison.OrdinalIgnoreCase)
            || finalPath.StartsWith("\\Device\\LanmanRedirector\\", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Restore paths on remote or shared Windows filesystems are not trusted.");
        }

        var volumeRoot = Path.GetPathRoot(finalPath);
        var driveType = volumeRoot is null ? 0 : GetDriveTypeW(volumeRoot);
        if (driveType != WindowsDriveTypeFixed)
        {
            throw new UnauthorizedAccessException("Restore requires a recognized local fixed Windows filesystem.");
        }

        var filesystemName = new System.Text.StringBuilder(32);
        if (!GetVolumeInformationByHandleW(handle, null, 0, out _, out _, out _, filesystemName, (uint)filesystemName.Capacity))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not identify the restore filesystem from its open handle.");
        }

        if (!IsTrustedWindowsFilesystem(filesystemName.ToString()))
        {
            throw new UnauthorizedAccessException("Restore requires a recognized local Windows filesystem; shared or unknown filesystems are not trusted.");
        }

        var error = GetSecurityInfo(
            handle,
            FileObjectSecurityType,
            OwnerSecurityInformation | DaclSecurityInformation,
            out _,
            out _,
            out _,
            out _,
            out var descriptor);
        if (error != 0)
        {
            throw new Win32Exception((int)error, "Could not read the restore directory owner and DACL from its handle.");
        }

        try
        {
            if (!GetSecurityDescriptorOwner(descriptor, out var owner, out _)
                || owner == IntPtr.Zero
                || !IsTrustedWindowsOwnerSid(GetSidString(owner), trustedSids, isFilesystemAnchor))
            {
                throw new UnauthorizedAccessException("The restore directory owner is not the service, SYSTEM, or an administrator.");
            }

            if (!GetSecurityDescriptorDacl(descriptor, out var daclPresent, out var dacl, out _)
                || daclPresent == 0
                || dacl == IntPtr.Zero)
            {
                throw new UnauthorizedAccessException("The restore directory has no enforceable DACL.");
            }

            var aclInfo = new AclSizeInformation();
            if (!GetAclInformation(dacl, out aclInfo, (uint)Marshal.SizeOf<AclSizeInformation>(), 2))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not inspect the restore directory DACL.");
            }

            for (uint index = 0; index < aclInfo.AceCount; index++)
            {
                if (!GetAce(dacl, index, out var ace))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not read an ACE from the restore directory DACL.");
                }

                var aceType = Marshal.ReadByte(ace);
                var aceFlags = Marshal.ReadByte(ace, 1);
                if (aceType is 1 or 6 or 10 or 12)
                {
                    continue;
                }

                var isAllowedAce = aceType is 0 or 5 or 9 or 11;
                if (!isAllowedAce)
                {
                    throw new UnauthorizedAccessException("The restore directory DACL contains an unsupported ACE type.");
                }

                var accessMask = unchecked((uint)Marshal.ReadInt32(ace, 4));
                var writeRights = isNamespaceParent ? WindowsNamespaceParentWriteRights : WindowsWriteRights;
                if ((accessMask & writeRights) == 0)
                {
                    continue;
                }

                var appliesOnlyToChildren = (aceFlags & 0x08) != 0;
                var appliesToChildren = (aceFlags & (0x01 | 0x02)) != 0;
                if (appliesOnlyToChildren && !(isFinalDirectory && appliesToChildren))
                {
                    continue;
                }

                var sidOffset = 8;
                if (aceType is 5 or 11)
                {
                    var objectFlags = unchecked((uint)Marshal.ReadInt32(ace, 8));
                    sidOffset = 12;
                    if ((objectFlags & 0x01) != 0)
                    {
                        sidOffset += 16;
                    }

                    if ((objectFlags & 0x02) != 0)
                    {
                        sidOffset += 16;
                    }
                }

                var aceSize = unchecked((ushort)Marshal.ReadInt16(ace, 2));
                if (sidOffset >= aceSize)
                {
                    throw new UnauthorizedAccessException("The restore directory DACL contains a malformed write ACE.");
                }

                if (!trustedSids.Contains(GetSidString(IntPtr.Add(ace, sidOffset))))
                {
                    throw new UnauthorizedAccessException("The restore directory DACL grants write access to an untrusted identity.");
                }
            }
        }
        finally
        {
            _ = LocalFree(descriptor);
        }
    }

    internal static bool IsTrustedWindowsOwnerSid(string sid, HashSet<string> trustedSids, bool isFilesystemAnchor)
        => trustedSids.Contains(sid)
            || (isFilesystemAnchor && string.Equals(sid, WindowsTrustedInstallerSid, StringComparison.Ordinal));

    private static string GetFinalPath(SafeFileHandle handle)
    {
        var requiredLength = GetFinalPathNameByHandleW(handle, null, 0, 0);
        if (requiredLength == 0 || requiredLength > int.MaxValue)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not determine the filesystem backing a restore directory handle.");
        }

        var finalPath = new System.Text.StringBuilder((int)requiredLength + 1);
        if (GetFinalPathNameByHandleW(handle, finalPath, (uint)finalPath.Capacity, 0) == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not resolve the filesystem for a restore directory handle.");
        }

        return finalPath.ToString();
    }

    private static string GetSidString(IntPtr sid)
    {
        if (!ConvertSidToStringSidW(sid, out var sidString))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not convert a Windows SID for restore ACL validation.");
        }

        try
        {
            return Marshal.PtrToStringUni(sidString)
                ?? throw new UnauthorizedAccessException("Could not read a Windows SID for restore ACL validation.");
        }
        finally
        {
            _ = LocalFree(sidString);
        }
    }

    private static void ValidateLinuxDirectory(SafeFileHandle handle, uint effectiveUserId)
    {
        using var readHandle = OpenLinuxDirectoryForMetadata(handle);
        var descriptorInfo = new LinuxStatx();
        const uint requiredStatxMask = 0x0000000B;
        if (Statx(readHandle, "", LinuxAtEmptyPath, LinuxStatxBasicStats, ref descriptorInfo) != 0
            || (descriptorInfo.Mask & requiredStatxMask) != requiredStatxMask)
        {
            throw new IOException("Could not read complete owner, mode, and type metadata for a restore directory handle.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        var owner = descriptorInfo.Uid;
        if (owner != effectiveUserId && owner != 0)
        {
            throw new UnauthorizedAccessException("The restore directory owner is not the service or root.");
        }

        if ((descriptorInfo.Mode & 0x0012) != 0)
        {
            throw new UnauthorizedAccessException("The restore directory grants group or other write access.");
        }

        var filesystemType = GetLinuxFilesystemType(readHandle);
        if (!IsLocalLinuxFilesystem(filesystemType))
        {
            throw new UnauthorizedAccessException("The restore directory is on an unrecognized or shared filesystem.");
        }

        ValidateLinuxAcl(readHandle, "system.posix_acl_access", effectiveUserId);
        ValidateLinuxAcl(readHandle, "system.posix_acl_default", effectiveUserId);
    }

    private static SafeFileHandle OpenLinuxDirectoryForMetadata(SafeFileHandle handle)
    {
        var descriptor = OpenAt(handle.DangerousGetHandle().ToInt32(), ".", LinuxOpenDirectory | LinuxOpenCloseOnExec);
        if (descriptor < 0)
        {
            throw new IOException("Could not open a restore directory handle for metadata validation.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
    }

    private static long GetLinuxFilesystemType(SafeFileHandle handle)
    {
        var buffer = Marshal.AllocHGlobal(256);
        try
        {
            if (FStatFs(handle, buffer) != 0)
            {
                throw new IOException("Could not identify the filesystem backing a restore directory handle.", new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            return Marshal.ReadInt64(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ValidateLinuxAcl(SafeFileHandle handle, string attributeName, uint effectiveUserId)
    {
        var size = FGetXAttr(handle, attributeName, IntPtr.Zero, 0);
        if (size < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == LinuxErrorNoData)
            {
                return;
            }

            throw new IOException($"Could not query {attributeName} through the restore directory handle; ACL validation fails closed.", new Win32Exception(error));
        }

        if (size < sizeof(uint) || size > 65536)
        {
            throw new UnauthorizedAccessException($"The restore directory has an invalid {attributeName} ACL.");
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (FGetXAttr(handle, attributeName, buffer, (nuint)size) != size)
            {
                throw new IOException($"Could not read {attributeName} through the restore directory handle.", new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            ValidateLinuxAclBytes(buffer, (int)size, attributeName, effectiveUserId);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static void ValidateLinuxAclBytes(IntPtr buffer, int length, string attributeName, uint effectiveUserId)
    {
        const uint aclVersion = 2;
        if (Marshal.ReadInt32(buffer) != aclVersion || (length - sizeof(uint)) % 8 != 0)
        {
            throw new UnauthorizedAccessException($"The restore directory has an unsupported {attributeName} ACL format.");
        }

        var entries = new List<(ushort Tag, ushort Permissions, uint Id)>();
        var hasNamedEntries = false;
        for (var offset = sizeof(uint); offset < length; offset += 8)
        {
            var bytes = new byte[8];
            Marshal.Copy(IntPtr.Add(buffer, offset), bytes, 0, bytes.Length);
            var tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2));
            var permissions = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2));
            var id = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
            if (permissions > 7 || tag is not (LinuxAclUserObject or LinuxAclUser or LinuxAclGroupObject or LinuxAclGroup or LinuxAclMask or LinuxAclOther))
            {
                throw new UnauthorizedAccessException($"The restore directory has an invalid {attributeName} ACL entry.");
            }

            hasNamedEntries |= tag is LinuxAclUser or LinuxAclGroup;
            entries.Add((tag, permissions, id));
        }

        var masks = entries.FindAll(entry => entry.Tag == LinuxAclMask);
        if (masks.Count > 1 || (hasNamedEntries && masks.Count != 1))
        {
            throw new UnauthorizedAccessException($"The restore directory has an invalid {attributeName} ACL mask.");
        }

        var mask = masks.Count == 0 ? (ushort)7 : masks[0].Permissions;
        if (entries.FindAll(entry => entry.Tag == LinuxAclUserObject).Count != 1
            || entries.FindAll(entry => entry.Tag == LinuxAclGroupObject).Count != 1
            || entries.FindAll(entry => entry.Tag == LinuxAclOther).Count != 1)
        {
            throw new UnauthorizedAccessException($"The restore directory has an incomplete {attributeName} ACL.");
        }

        foreach (var entry in entries)
        {
            var effectivePermissions = entry.Tag is LinuxAclUserObject or LinuxAclOther
                ? entry.Permissions
                : (ushort)(entry.Permissions & mask);
            if ((effectivePermissions & LinuxAclWrite) == 0)
            {
                continue;
            }

            if (entry.Tag is LinuxAclGroup or LinuxAclGroupObject or LinuxAclOther
                || (entry.Tag == LinuxAclUser && entry.Id != effectiveUserId && entry.Id != 0))
            {
                throw new UnauthorizedAccessException($"The restore directory {attributeName} ACL grants write access outside the service/root identities.");
            }
        }
    }

    private static uint GetEffectiveUserId()
    {
        var userId = GetEuid();
        if (userId == uint.MaxValue)
        {
            throw new IOException("Could not identify the restore service UID.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return userId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStatx
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(20)]
        public uint Uid;

        [FieldOffset(28)]
        public ushort Mode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AclSizeInformation
    {
        public uint AceCount;
        public uint AclBytesInUse;
        public uint AclBytesFree;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, uint objectType, uint securityInfo, out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr securityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out SafeFileHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeFileHandle token, int tokenInformationClass, IntPtr tokenInformation, uint tokenInformationLength, out uint returnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorOwner(IntPtr securityDescriptor, out IntPtr owner, out int ownerDefaulted);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorDacl(IntPtr securityDescriptor, out int daclPresent, out IntPtr dacl, out int daclDefaulted);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetAclInformation(IntPtr acl, out AclSizeInformation buffer, uint bufferLength, int informationClass);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetAce(IntPtr acl, uint aceIndex, out IntPtr ace);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr sidString);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, System.Text.StringBuilder? path, uint length, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetDriveTypeW(string? rootPathName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(
        SafeFileHandle handle,
        System.Text.StringBuilder? volumeName,
        uint volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        System.Text.StringBuilder fileSystemName,
        uint fileSystemNameSize);

    [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
    private static extern uint GetEuid();

    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAt(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "fstatfs", SetLastError = true)]
    private static extern int FStatFs(SafeFileHandle file, IntPtr buffer);

    [DllImport("libc", EntryPoint = "fgetxattr", SetLastError = true)]
    private static extern nint FGetXAttr(SafeFileHandle file, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr value, nuint size);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(SafeFileHandle file, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, ref LinuxStatx buffer);
}
