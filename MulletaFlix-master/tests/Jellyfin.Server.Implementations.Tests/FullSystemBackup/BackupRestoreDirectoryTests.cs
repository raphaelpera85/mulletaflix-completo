using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.FullSystemBackup;

public sealed class BackupRestoreDirectoryTests
{
    [Theory]
    [InlineData(0x0000EF53, true)] // ext filesystem
    [InlineData(0x794C7630, false)] // overlay backing cannot be verified from fstatfs alone
    public void IsLocalLinuxFilesystem_RejectsUnverifiableOverlayBacking(long filesystemType, bool expected)
    {
        Assert.Equal(expected, BackupRestoreAclValidator.IsLocalLinuxFilesystem(filesystemType));
    }

    [Theory]
    [InlineData("NTFS", true)]
    [InlineData("ReFS", true)]
    [InlineData("CSVFS", false)]
    [InlineData("unknown", false)]
    public void IsTrustedWindowsFilesystem_RejectsSharedAndUnknownFilesystems(string filesystemName, bool expected)
    {
        Assert.Equal(expected, BackupRestoreAclValidator.IsTrustedWindowsFilesystem(filesystemName));
    }

    [Fact]
    public void IsTrustedWindowsOwnerSid_AllowsTrustedInstallerOnlyForFilesystemAnchor()
    {
        var trustedWriteSids = new HashSet<string>(StringComparer.Ordinal)
        {
            "S-1-5-18",
            "S-1-5-32-544"
        };
        const string trustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

        Assert.True(BackupRestoreAclValidator.IsTrustedWindowsOwnerSid("S-1-5-18", trustedWriteSids, isFilesystemAnchor: false));
        Assert.True(BackupRestoreAclValidator.IsTrustedWindowsOwnerSid(trustedInstallerSid, trustedWriteSids, isFilesystemAnchor: true));
        Assert.False(BackupRestoreAclValidator.IsTrustedWindowsOwnerSid(trustedInstallerSid, trustedWriteSids, isFilesystemAnchor: false));
        Assert.False(trustedWriteSids.Contains(trustedInstallerSid));
    }

    [Fact]
    public void FileExists_DoesNotTreatAccessDeniedAsMissingFileOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "The access-denied file ACL case requires Windows.");
        using var temporary = new TemporaryDirectory();
        var filePath = System.IO.Path.Combine(temporary.Path, "existing.txt");
        File.WriteAllText(filePath, "preserve-me");
        var fileInfo = new FileInfo(filePath);
        var originalSecurity = fileInfo.GetAccessControl();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        try
        {
            var security = fileInfo.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User!,
                FileSystemRights.ReadData,
                AccessControlType.Deny));
            fileInfo.SetAccessControl(security);

            var error = Assert.Throws<IOException>(() => root.FileExists("existing.txt"));
            Assert.Equal(5, Assert.IsType<System.ComponentModel.Win32Exception>(error.InnerException).NativeErrorCode);
        }
        finally
        {
            fileInfo.SetAccessControl(originalSecurity);
        }

        fileInfo.Refresh();
        Assert.True(fileInfo.Exists);
        Assert.Equal("preserve-me".Length, fileInfo.Length);
    }

    [Fact]
    public void ValidateTrustedWriteBoundary_AcceptsPrivateDirectory()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore trust-boundary validation supports Windows and Linux.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        root.ValidateTrustedWriteBoundary();
    }

    [Fact]
    public void ValidateTrustedWriteBoundary_RejectsUntrustedWriter()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore trust-boundary validation supports Windows and Linux.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        if (OperatingSystem.IsWindows())
        {
            var directoryInfo = new DirectoryInfo(temporary.Path);
            var originalSecurity = directoryInfo.GetAccessControl();
            try
            {
                var security = directoryInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                directoryInfo.SetAccessControl(security);

                Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedWriteBoundary);
            }
            finally
            {
                directoryInfo.SetAccessControl(originalSecurity);
            }
        }
        else
        {
            var originalMode = File.GetUnixFileMode(temporary.Path);
            try
            {
                File.SetUnixFileMode(temporary.Path, originalMode | UnixFileMode.OtherWrite);

                Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedWriteBoundary);
            }
            finally
            {
                File.SetUnixFileMode(temporary.Path, originalMode);
            }
        }
    }

    [Fact]
    public void OpenRoot_RejectsUntrustedWritableAncestor()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore trust-boundary validation supports Windows and Linux.");
        using var temporary = new TemporaryDirectory();
        var untrustedAncestor = Path.Combine(temporary.Path, "untrusted-ancestor");
        var restoreRoot = Path.Combine(untrustedAncestor, "restore-root");
        Directory.CreateDirectory(restoreRoot);

        if (OperatingSystem.IsWindows())
        {
            var directoryInfo = new DirectoryInfo(untrustedAncestor);
            var originalSecurity = directoryInfo.GetAccessControl();
            try
            {
                var security = directoryInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                directoryInfo.SetAccessControl(security);

                Assert.Throws<UnauthorizedAccessException>(() =>
                {
                    using var restoreAuthority = BackupRestoreDirectory.OpenRoot(restoreRoot);
                });
            }
            finally
            {
                directoryInfo.SetAccessControl(originalSecurity);
            }
        }
        else
        {
            var originalMode = File.GetUnixFileMode(untrustedAncestor);
            try
            {
                File.SetUnixFileMode(untrustedAncestor, originalMode | UnixFileMode.OtherWrite);

                Assert.Throws<UnauthorizedAccessException>(() =>
                {
                    using var restoreAuthority = BackupRestoreDirectory.OpenRoot(restoreRoot);
                });
            }
            finally
            {
                File.SetUnixFileMode(untrustedAncestor, originalMode);
            }
        }
    }

    [Fact]
    public void OpenRoot_PreventsRenamingTheRootWhileAuthorityIsOpen()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows directory sharing semantics are required.");
        using var temporary = new TemporaryDirectory();
        var rootPath = Path.Combine(temporary.Path, "restore-root");
        var movedPath = Path.Combine(temporary.Path, "restore-root-moved");
        Directory.CreateDirectory(rootPath);

        Exception? renameError;
        using (BackupRestoreDirectory.OpenRoot(rootPath))
        {
            renameError = Record.Exception(() => Directory.Move(rootPath, movedPath));
            if (!Directory.Exists(rootPath) && Directory.Exists(movedPath))
            {
                Directory.Move(movedPath, rootPath);
            }
        }

        Assert.True(renameError is IOException or UnauthorizedAccessException, $"Expected an open restore handle to block renaming the root, but got: {renameError}");
        Directory.Move(rootPath, movedPath);
        Directory.Move(movedPath, rootPath);
    }

    [Fact]
    public void OpenRoot_PreventsRenamingAnAncestorWhileAuthorityIsOpen()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows directory sharing semantics are required.");
        using var temporary = new TemporaryDirectory();
        var ancestorPath = Path.Combine(temporary.Path, "configured");
        var movedPath = Path.Combine(temporary.Path, "configured-moved");
        var rootPath = Path.Combine(ancestorPath, "restore-root");
        Directory.CreateDirectory(rootPath);

        Exception? renameError;
        using (BackupRestoreDirectory.OpenRoot(rootPath))
        {
            renameError = Record.Exception(() => Directory.Move(ancestorPath, movedPath));
            if (!Directory.Exists(ancestorPath) && Directory.Exists(movedPath))
            {
                Directory.Move(movedPath, ancestorPath);
            }
        }

        Assert.True(renameError is IOException or UnauthorizedAccessException, $"Expected an open restore authority to block renaming an ancestor, but got: {renameError}");
        Assert.True(Directory.Exists(rootPath));
        Assert.False(Directory.Exists(movedPath));
    }

    [Fact]
    public void PhysicallyOverlaps_DetectsSameDirectoryAndAncestorAcrossHandles()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore directory identity supports Windows and Linux.");
        using var temporary = new TemporaryDirectory();
        var childPath = Path.Combine(temporary.Path, "child");
        Directory.CreateDirectory(childPath);

        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var sameRoot = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var child = BackupRestoreDirectory.OpenRoot(childPath);

        Assert.True(root.PhysicallyOverlaps(sameRoot));
        Assert.True(root.IsSameOrAncestorOf(child));
        Assert.True(root.PhysicallyOverlaps(child));
        Assert.False(child.IsSameOrAncestorOf(root));
    }

    [Fact]
    public void IsSameDirectoryAs_UsesOpenedDirectoryIdentity()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore directory identity supports Windows and Linux.");
        using var temporary = new TemporaryDirectory();
        var childPath = Path.Combine(temporary.Path, "child");
        var siblingPath = Path.Combine(temporary.Path, "sibling");
        Directory.CreateDirectory(childPath);
        Directory.CreateDirectory(siblingPath);

        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var child = root.OpenDirectory("child");
        using var sameChild = BackupRestoreDirectory.OpenRoot(childPath);
        using var sibling = root.OpenDirectory("sibling");

        Assert.True(child.IsSameDirectoryAs(sameChild));
        Assert.False(child.IsSameDirectoryAs(sibling));
    }

    [Fact]
    public void TryParseLinuxMountInfoLine_DecodesEscapedPaths()
    {
        const string line = "42 1 8:1 /restore\\040root /mnt\\040alias rw - ext4 /dev/sda1 rw";

        Assert.True(BackupRestoreDirectory.TryParseLinuxMountInfoLine(line, out var entry));
        Assert.Equal(42UL, entry.MountId);
        Assert.Equal(8U, entry.DeviceMajor);
        Assert.Equal(1U, entry.DeviceMinor);
        Assert.Equal("/restore root", entry.Root);
        Assert.Equal("/mnt alias", entry.MountPoint);
    }

    [Fact]
    public void IsSameOrAncestorOf_DetectsBindMountedProgramDataInsideRestoreRoot()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Bind-mount source identity requires Linux.");
        using var temporary = new TemporaryDirectory();
        var restoreRootPath = Path.Combine(temporary.Path, "restore");
        var source = Path.Combine(restoreRootPath, "program-data-source");
        var target = Path.Combine(temporary.Path, "program-data");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);

        using var restoreRoot = BackupRestoreDirectory.OpenRoot(restoreRootPath);
        if (Mount(source, target, null, 4096, IntPtr.Zero) != 0)
        {
            Assert.Skip($"Linux bind mounts are unavailable in this environment (errno {Marshal.GetLastPInvokeError()}).");
        }

        try
        {
            using var programData = BackupRestoreDirectory.OpenRoot(target);
            Assert.True(restoreRoot.IsSameOrAncestorOf(programData));
        }
        finally
        {
            if (Unmount(target, 0) != 0)
            {
                throw new IOException("Could not unmount the temporary restore test bind mount.", new Win32Exception(Marshal.GetLastPInvokeError()));
            }
        }
    }

    [Fact]
    public void IsOnSameMountAs_DetectsBindMountedDirectory()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Bind-mount identity requires Linux.");
        using var temporary = new TemporaryDirectory();
        var source = Path.Combine(temporary.Path, "source");
        var target = Path.Combine(temporary.Path, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);

        using var targetRoot = BackupRestoreDirectory.OpenRoot(target);
        if (Mount(source, target, null, 4096, IntPtr.Zero) != 0)
        {
            Assert.Skip($"Linux bind mounts are unavailable in this environment (errno {Marshal.GetLastPInvokeError()}).");
        }

        try
        {
            using var mountedRoot = BackupRestoreDirectory.OpenRoot(target);
            Assert.False(targetRoot.IsOnSameMountAs(mountedRoot));
        }
        finally
        {
            if (Unmount(target, 0) != 0)
            {
                throw new IOException("Could not unmount the temporary restore test bind mount.", new Win32Exception(Marshal.GetLastPInvokeError()));
            }
        }
    }

    [Fact]
    public void ValidateTrustedWriteBoundary_RejectsWritableNamedUserInLinuxDefaultAcl()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "POSIX default ACL validation requires Linux.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        SetLinuxDefaultAcl(temporary.Path,
        [
            (0x0001, 7, uint.MaxValue), // user::rwx
            (0x0002, 2, uint.MaxValue - 1), // user:<untrusted>:w--
            (0x0004, 5, uint.MaxValue), // group::r-x
            (0x0010, 7, uint.MaxValue), // mask::rwx
            (0x0020, 5, uint.MaxValue), // other::r-x
        ]);

        Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedWriteBoundary);
    }

    [Fact]
    public void ValidateLinuxAclBytes_RejectsOtherWriteEvenWhenMaskOmitsIt()
    {
        var entries = new (ushort Tag, ushort Permissions, uint Id)[]
        {
            (0x0001, 7, uint.MaxValue), // user::rwx
            (0x0002, 7, uint.MaxValue - 1), // user:<untrusted>:rwx
            (0x0004, 5, uint.MaxValue), // group::r-x
            (0x0010, 5, uint.MaxValue), // mask::r-x
            (0x0020, 2, uint.MaxValue), // other::-w-
        };
        var acl = new byte[sizeof(uint) + (entries.Length * 8)];
        BinaryPrimitives.WriteUInt32LittleEndian(acl, 2);
        for (var index = 0; index < entries.Length; index++)
        {
            var offset = sizeof(uint) + (index * 8);
            BinaryPrimitives.WriteUInt16LittleEndian(acl.AsSpan(offset), entries[index].Tag);
            BinaryPrimitives.WriteUInt16LittleEndian(acl.AsSpan(offset + 2), entries[index].Permissions);
            BinaryPrimitives.WriteUInt32LittleEndian(acl.AsSpan(offset + 4), entries[index].Id);
        }

        var buffer = Marshal.AllocHGlobal(acl.Length);
        try
        {
            Marshal.Copy(acl, 0, buffer, acl.Length);
            Assert.Throws<UnauthorizedAccessException>(() => BackupRestoreAclValidator.ValidateLinuxAclBytes(buffer, acl.Length, "system.posix_acl_default", 1000));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void ValidateTrustedWriteBoundary_RejectsOtherWriteInLinuxDefaultAcl()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "POSIX default ACL validation requires Linux.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        SetLinuxDefaultAcl(temporary.Path,
        [
            (0x0001, 7, uint.MaxValue), // user::rwx
            (0x0002, 7, uint.MaxValue - 1), // user:<untrusted>:rwx
            (0x0004, 5, uint.MaxValue), // group::r-x
            (0x0010, 5, uint.MaxValue), // mask::r-x
            (0x0020, 2, uint.MaxValue), // other::-w-
        ]);

        Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedWriteBoundary);
    }

    [Fact]
    public void ValidateTrustedWriteBoundary_RejectsUntrustedDirectoryCreationOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows ACL validation must reject untrusted directory-creation rights.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        var directoryInfo = new DirectoryInfo(temporary.Path);
        var originalSecurity = directoryInfo.GetAccessControl();
        root.ValidateTrustedWriteBoundary();

        try
        {
            var security = directoryInfo.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.CreateDirectories,
                AccessControlType.Allow));
            directoryInfo.SetAccessControl(security);

            Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedWriteBoundary);
        }
        finally
        {
            directoryInfo.SetAccessControl(originalSecurity);
        }
    }

    [Fact]
    public void ValidateTrustedWriteBoundary_RejectsTrustedInstallerWriteGrantOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows ACL validation must reject write grants to TrustedInstaller.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        var directoryInfo = new DirectoryInfo(temporary.Path);
        var originalSecurity = directoryInfo.GetAccessControl();
        var trustedInstaller = new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");

        try
        {
            var security = directoryInfo.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                trustedInstaller,
                FileSystemRights.CreateFiles,
                AccessControlType.Allow));
            directoryInfo.SetAccessControl(security);

            Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedWriteBoundary);
        }
        finally
        {
            directoryInfo.SetAccessControl(originalSecurity);
        }
    }

    [Fact]
    public void ValidateTrustedNamespaceParent_AllowsDirectoryCreationButRejectsUntrustedFileAndReplacementRights()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Restore trust-boundary validation supports Windows and Linux.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        if (OperatingSystem.IsWindows())
        {
            var directoryInfo = new DirectoryInfo(temporary.Path);
            var originalSecurity = directoryInfo.GetAccessControl();
            var world = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            try
            {
                var security = directoryInfo.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(world, FileSystemRights.CreateDirectories, AccessControlType.Allow));
                directoryInfo.SetAccessControl(security);
                root.ValidateTrustedNamespaceParent();

                security = directoryInfo.GetAccessControl();
                security.RemoveAccessRuleAll(new FileSystemAccessRule(world, FileSystemRights.CreateDirectories, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(world, FileSystemRights.CreateFiles, AccessControlType.Allow));
                directoryInfo.SetAccessControl(security);
                Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedNamespaceParent);

                security = directoryInfo.GetAccessControl();
                security.RemoveAccessRuleAll(new FileSystemAccessRule(world, FileSystemRights.CreateFiles, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(world, FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Allow));
                directoryInfo.SetAccessControl(security);
                Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedNamespaceParent);
            }
            finally
            {
                directoryInfo.SetAccessControl(originalSecurity);
            }
        }
        else
        {
            var originalMode = File.GetUnixFileMode(temporary.Path);
            try
            {
                File.SetUnixFileMode(temporary.Path, originalMode | UnixFileMode.OtherWrite);
                Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedNamespaceParent);
            }
            finally
            {
                File.SetUnixFileMode(temporary.Path, originalMode);
            }
        }
    }

    [Fact]
    public void ValidateTrustedWriteBoundary_RejectsUntrustedContainerInheritOnlyWriterOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows ACL validation must reject untrusted write grants inherited by child directories.");
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        var directoryInfo = new DirectoryInfo(temporary.Path);
        var originalSecurity = directoryInfo.GetAccessControl();

        try
        {
            var security = directoryInfo.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));
            directoryInfo.SetAccessControl(security);

            Assert.Throws<UnauthorizedAccessException>(root.ValidateTrustedWriteBoundary);
        }
        finally
        {
            directoryInfo.SetAccessControl(originalSecurity);
        }
    }

    [Fact]
    public void OpenDirectory_TraversesEachComponentFromOpenedHandle()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "one", "two"));

        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var one = root.OpenDirectory("one");
        using var two = one.OpenDirectory("two");

        Assert.NotNull(two);
    }

    [Fact]
    public void OpenRoot_RetainsLinuxAncestorHandlesUntilDisposed()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Linux root traversal retains each opened ancestor descriptor.");
        Assert.SkipWhen(!Directory.Exists("/proc/self/fd"), "The test requires procfs to inspect open descriptors.");

        using var temporary = new TemporaryDirectory();
        var parentPath = Path.Combine(temporary.Path, "parent");
        var rootPath = Path.Combine(parentPath, "root");
        Directory.CreateDirectory(rootPath);
        var openHandlesBefore = CountOpenDirectoryHandles(parentPath);

        using (BackupRestoreDirectory.OpenRoot(rootPath))
        {
            Assert.Equal(openHandlesBefore + 1, CountOpenDirectoryHandles(parentPath));
        }

        Assert.Equal(openHandlesBefore, CountOpenDirectoryHandles(parentPath));
    }

    [Fact]
    public void CreateDirectory_CreatesOnlyNewRelativeChild()
    {
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        using var child = root.CreateDirectory("created");

        Assert.True(Directory.Exists(Path.Combine(temporary.Path, "created")));
        Assert.ThrowsAny<Exception>(() => root.CreateDirectory("created"));
    }

    [Fact]
    public void OpenOrCreateDirectoryAndFile_ReuseExistingEntriesAndRejectLinks()
    {
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var recovery = root.OpenOrCreateDirectory("recovery");
        using var reopened = root.OpenOrCreateDirectory("recovery");
        using var first = recovery.OpenOrCreateFile("lease.lock");
        using var second = reopened.OpenOrCreateFile("lease.lock");
        first.Write("preserve"u8);
        first.Flush();
        first.Lock(0, 1);
        Assert.Throws<IOException>(() => second.Lock(0, 1));
        first.Unlock(0, 1);
        second.Lock(0, 1);
        second.Unlock(0, 1);

        recovery.DeleteFileIfExists("missing.file");
        using (var read = recovery.OpenExistingFile("lease.lock"))
        {
            Assert.Equal("preserve", new StreamReader(read).ReadToEnd());
        }

        Assert.True(File.Exists(Path.Combine(temporary.Path, "recovery", "lease.lock")));
    }

    [Fact]
    public void OpenOrCreateFileAndDeleteIfExists_RejectSymbolicLinkOnLinux()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Linux opens files with O_NOFOLLOW and validates type through the opened descriptor.");

        using var temporary = new TemporaryDirectory();
        var outsidePath = Path.Combine(temporary.Path, "outside.bin");
        var linkPath = Path.Combine(temporary.Path, "linked.bin");
        var outsideDirectoryPath = Path.Combine(temporary.Path, "outside-directory");
        var linkedDirectoryPath = Path.Combine(temporary.Path, "linked-directory");
        File.WriteAllText(outsidePath, "preserve");
        Directory.CreateDirectory(outsideDirectoryPath);
        File.CreateSymbolicLink(linkPath, outsidePath);
        Directory.CreateSymbolicLink(linkedDirectoryPath, outsideDirectoryPath);
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.ThrowsAny<Exception>(() => root.OpenOrCreateFile("linked.bin"));
        Assert.ThrowsAny<Exception>(() => root.OpenOrCreateDirectory("linked-directory"));
        Assert.Throws<IOException>(() => root.DeleteFileIfExists("linked.bin"));
        Assert.Equal("preserve", File.ReadAllText(outsidePath));
        Assert.True(File.Exists(linkPath));
    }

    [Fact]
    public void CreateNewFile_IsExclusiveAndWritesThroughOpenedHandle()
    {
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        using (var stream = root.CreateNewFile("data.bin"))
        {
            stream.Write("payload"u8);
        }

        Assert.Equal("payload", File.ReadAllText(Path.Combine(temporary.Path, "data.bin")));
        Assert.ThrowsAny<Exception>(() => root.CreateNewFile("data.bin"));
    }

    [Fact]
    public void OpenExistingFile_UsesValidatedHandleAndRequestedAccess()
    {
        using var temporary = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(temporary.Path, "data.bin"), "payload");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        using (var read = root.OpenExistingFile("data.bin"))
        using (var reader = new StreamReader(read))
        {
            Assert.Equal("payload", reader.ReadToEnd());
        }

        using (var readWrite = root.OpenExistingFile("data.bin", FileAccess.ReadWrite))
        {
            readWrite.Position = 0;
            readWrite.Write("updated"u8);
        }

        Assert.Equal("updated", File.ReadAllText(Path.Combine(temporary.Path, "data.bin")));
    }

    [Fact]
    public void OpenExistingFile_RejectsMissingEntriesAndDirectories()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "directory"));
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.ThrowsAny<Exception>(() => root.OpenExistingFile("missing.bin"));
        Assert.ThrowsAny<Exception>(() => root.OpenExistingFile("directory"));
    }

    [Fact]
    public void OpenExistingFile_RejectsSymbolicLinkOnLinux()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Linux open uses O_NOFOLLOW and validates the opened handle with statx.");

        using var temporary = new TemporaryDirectory();
        var targetPath = Path.Combine(temporary.Path, "outside.bin");
        File.WriteAllText(targetPath, "preserve");
        File.CreateSymbolicLink(Path.Combine(temporary.Path, "link.bin"), targetPath);
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.ThrowsAny<Exception>(() => root.OpenExistingFile("link.bin"));
        Assert.Equal("preserve", File.ReadAllText(targetPath));
    }

    [Fact]
    public void OpenExistingFile_PreventsReplacingOpenedFileOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "The Windows handle intentionally omits FILE_SHARE_DELETE.");

        using var temporary = new TemporaryDirectory();
        var filePath = Path.Combine(temporary.Path, "data.bin");
        var movedPath = Path.Combine(temporary.Path, "moved.bin");
        File.WriteAllText(filePath, "trusted");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var file = root.OpenExistingFile("data.bin");

        Assert.ThrowsAny<IOException>(() => File.Move(filePath, movedPath));
        Assert.Equal("trusted", File.ReadAllText(filePath));
        Assert.False(File.Exists(movedPath));
    }

    [Fact]
    public void OpenExistingFile_CanDenyConcurrentWritersOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows file sharing controls concurrent writers.");

        using var temporary = new TemporaryDirectory();
        var path = Path.Combine(temporary.Path, "archive.zip");
        File.WriteAllText(path, "archive-content");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var read = root.OpenExistingFile("archive.zip", FileAccess.Read, FileShare.Read);

        Assert.ThrowsAny<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
    }

    [Fact]
    public void Rename_UsesOpenedSourceAndDestinationDirectories()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "source"));
        Directory.CreateDirectory(Path.Combine(temporary.Path, "destination"));
        File.WriteAllText(Path.Combine(temporary.Path, "source", "item.bin"), "value");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var source = root.OpenDirectory("source");
        using var destination = root.OpenDirectory("destination");

        source.Rename("item.bin", destination, "renamed.bin");

        Assert.False(File.Exists(Path.Combine(temporary.Path, "source", "item.bin")));
        Assert.Equal("value", File.ReadAllText(Path.Combine(temporary.Path, "destination", "renamed.bin")));
    }

    [Fact]
    public void Rename_WithoutReplacePreservesExistingDestination()
    {
        using var temporary = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(temporary.Path, "source.bin"), "source");
        File.WriteAllText(Path.Combine(temporary.Path, "destination.bin"), "destination");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.ThrowsAny<Exception>(() => root.Rename("source.bin", root, "destination.bin"));

        Assert.Equal("source", File.ReadAllText(Path.Combine(temporary.Path, "source.bin")));
        Assert.Equal("destination", File.ReadAllText(Path.Combine(temporary.Path, "destination.bin")));
    }

    [Fact]
    public void Rename_RejectsSymbolicLinkSourceOnLinux()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Linux rename rejects symbolic-link entries before moving them.");

        using var temporary = new TemporaryDirectory();
        var targetPath = Path.Combine(temporary.Path, "outside.bin");
        var linkPath = Path.Combine(temporary.Path, "link.bin");
        File.WriteAllText(targetPath, "preserve");
        File.CreateSymbolicLink(linkPath, targetPath);
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.Throws<IOException>(() => root.Rename("link.bin", root, "moved.bin"));

        Assert.Equal("preserve", File.ReadAllText(targetPath));
        Assert.True((File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0);
        Assert.False(File.Exists(Path.Combine(temporary.Path, "moved.bin")));
    }

    [Fact]
    public void Rename_RejectsReparsePointSourceOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows rename rejects reparse-point entries before moving them.");

        using var temporary = new TemporaryDirectory();
        var targetPath = Path.Combine(temporary.Path, "target");
        var linkPath = Path.Combine(temporary.Path, "linked");
        Directory.CreateDirectory(targetPath);
        File.WriteAllText(Path.Combine(targetPath, "keep.bin"), "preserve");
        CreateDirectoryLink(linkPath, targetPath);
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.Throws<IOException>(() => root.Rename("linked", root, "moved"));

        Assert.Equal("preserve", File.ReadAllText(Path.Combine(targetPath, "keep.bin")));
        Assert.True((File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0);
        Assert.False(Directory.Exists(Path.Combine(temporary.Path, "moved")));
    }

    [Fact]
    public void Rename_WithReplaceReplacesExistingDestination()
    {
        using var temporary = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(temporary.Path, "source.bin"), "source");
        File.WriteAllText(Path.Combine(temporary.Path, "destination.bin"), "destination");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        root.Rename("source.bin", root, "destination.bin", replaceExisting: true);

        Assert.False(File.Exists(Path.Combine(temporary.Path, "source.bin")));
        Assert.Equal("source", File.ReadAllText(Path.Combine(temporary.Path, "destination.bin")));
    }

    [Fact]
    public void Rename_WithReplaceFailurePreservesSourceAndDestination()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Safe rename is supported only on Windows and Linux.");

        using var temporary = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(temporary.Path, "source.bin"), "source");
        var destinationPath = Path.Combine(temporary.Path, "destination");
        Directory.CreateDirectory(destinationPath);
        File.WriteAllText(Path.Combine(destinationPath, "sentinel.bin"), "destination");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.ThrowsAny<IOException>(() => root.Rename("source.bin", root, "destination", replaceExisting: true));

        Assert.Equal("source", File.ReadAllText(Path.Combine(temporary.Path, "source.bin")));
        Assert.Equal("destination", File.ReadAllText(Path.Combine(destinationPath, "sentinel.bin")));
    }

    [Fact]
    public void DeleteOperations_RemoveOnlyExpectedEntryTypes()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "empty"));
        File.WriteAllText(Path.Combine(temporary.Path, "file.bin"), "value");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        root.DeleteFile("file.bin");
        root.DeleteDirectory("empty");

        Assert.False(File.Exists(Path.Combine(temporary.Path, "file.bin")));
        Assert.False(Directory.Exists(Path.Combine(temporary.Path, "empty")));
    }

    [Fact]
    public void DeleteOperations_RejectMismatchedEntryTypes()
    {
        using var temporary = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temporary.Path, "directory"));
        File.WriteAllText(Path.Combine(temporary.Path, "file.bin"), "preserve");
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.Throws<IOException>(() => root.DeleteFile("directory"));
        Assert.Throws<IOException>(() => root.DeleteDirectory("file.bin"));

        Assert.True(Directory.Exists(Path.Combine(temporary.Path, "directory")));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(temporary.Path, "file.bin")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    public void RelativeOperations_RejectUnsafeOrMultiComponentNames(string component)
    {
        using var temporary = new TemporaryDirectory();
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.Throws<ArgumentException>(() => root.OpenDirectory(component));
        Assert.Throws<ArgumentException>(() => root.CreateDirectory(component));
        Assert.Throws<ArgumentException>(() => root.OpenOrCreateDirectory(component));
        Assert.Throws<ArgumentException>(() => root.CreateNewFile(component));
        Assert.Throws<ArgumentException>(() => root.OpenExistingFile(component));
        Assert.Throws<ArgumentException>(() => root.OpenOrCreateFile(component));
        Assert.Throws<ArgumentException>(() => root.DeleteFile(component));
        Assert.Throws<ArgumentException>(() => root.DeleteFileIfExists(component));
        Assert.Throws<ArgumentException>(() => root.DeleteNonDirectoryEntryIfExists(component));
        Assert.Throws<ArgumentException>(() => root.DeleteDirectory(component));
        Assert.Throws<ArgumentException>(() => root.Rename(component, root, "valid"));
        Assert.Throws<ArgumentException>(() => root.Rename("valid", root, component));
    }

    [Fact]
    public void OpenRootAndOpenDirectory_RejectDirectoryReparsePoints()
    {
        using var temporary = new TemporaryDirectory();
        var rootPath = Path.Combine(temporary.Path, "root");
        var outsidePath = Path.Combine(temporary.Path, "outside");
        var linkPath = Path.Combine(rootPath, "linked");
        var rootLinkPath = Path.Combine(temporary.Path, "root-link");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(outsidePath);
        CreateDirectoryLink(linkPath, outsidePath);
        CreateDirectoryLink(rootLinkPath, rootPath);

        using var root = BackupRestoreDirectory.OpenRoot(rootPath);

        Assert.ThrowsAny<Exception>(() => root.OpenDirectory("linked"));
        Assert.ThrowsAny<Exception>(() => BackupRestoreDirectory.OpenRoot(rootLinkPath));
        Assert.False(File.Exists(Path.Combine(outsidePath, "must-not-exist")));
    }

    [Fact]
    public void OpenRoot_RejectsSymbolicLinkInAncestorOnLinux()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Linux root traversal uses dirfd and O_NOFOLLOW.");

        using var temporary = new TemporaryDirectory();
        var realParent = Path.Combine(temporary.Path, "real-parent");
        var linkedParent = Path.Combine(temporary.Path, "linked-parent");
        var rootPath = Path.Combine(linkedParent, "restore-root");
        Directory.CreateDirectory(Path.Combine(realParent, "restore-root"));
        Directory.CreateSymbolicLink(linkedParent, realParent);

        Assert.ThrowsAny<Exception>(() => BackupRestoreDirectory.OpenRoot(rootPath));
    }

    [Fact]
    public void OpenRoot_RejectsReparsePointInAncestorOnWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows root traversal opens each component relative to its parent handle.");

        using var temporary = new TemporaryDirectory();
        var realParent = Path.Combine(temporary.Path, "real-parent");
        var linkedParent = Path.Combine(temporary.Path, "linked-parent");
        var rootPath = Path.Combine(linkedParent, "restore-root");
        Directory.CreateDirectory(Path.Combine(realParent, "restore-root"));
        CreateDirectoryLink(linkedParent, realParent);

        Assert.ThrowsAny<Exception>(() => BackupRestoreDirectory.OpenRoot(rootPath));
    }

    [Fact]
    public void DeleteFile_RejectsSymbolicLinkOnLinux()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Linux delete type validation uses statx.");

        using var temporary = new TemporaryDirectory();
        var targetPath = Path.Combine(temporary.Path, "outside.bin");
        var linkPath = Path.Combine(temporary.Path, "link.bin");
        File.WriteAllText(targetPath, "preserve");
        File.CreateSymbolicLink(linkPath, targetPath);
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        Assert.Throws<IOException>(() => root.DeleteFile("link.bin"));

        Assert.Equal("preserve", File.ReadAllText(targetPath));
        Assert.True((File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0);
    }

    [Fact]
    public void DeleteNonDirectoryEntryIfExists_RemovesLinkWithoutFollowingItOnLinux()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "Linux unlinkat removes the directory entry without following symbolic links.");

        using var temporary = new TemporaryDirectory();
        var targetPath = Path.Combine(temporary.Path, "outside.bin");
        var linkPath = Path.Combine(temporary.Path, "temporary-link");
        File.WriteAllText(targetPath, "preserve");
        File.CreateSymbolicLink(linkPath, targetPath);
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        root.DeleteNonDirectoryEntryIfExists("temporary-link");
        root.DeleteNonDirectoryEntryIfExists("missing-link");

        Assert.Equal("preserve", File.ReadAllText(targetPath));
        Assert.False(File.Exists(linkPath));
    }

    [Fact]
    public void DeleteNonDirectoryEntryIfExists_RemovesFilesAndRejectsDirectories()
    {
        using var temporary = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(temporary.Path, "temporary-file"), "temporary");
        Directory.CreateDirectory(Path.Combine(temporary.Path, "directory"));
        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);

        root.DeleteNonDirectoryEntryIfExists("temporary-file");
        root.DeleteNonDirectoryEntryIfExists("missing-file");

        Assert.False(File.Exists(Path.Combine(temporary.Path, "temporary-file")));
        Assert.ThrowsAny<Exception>(() => root.DeleteNonDirectoryEntryIfExists("directory"));
        Assert.True(Directory.Exists(Path.Combine(temporary.Path, "directory")));
    }

    [Fact]
    public void OpenDirectory_PreventsConcurrentRenameWhileHandleIsOpenOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var temporary = new TemporaryDirectory();
        var childPath = Path.Combine(temporary.Path, "child");
        var movedPath = Path.Combine(temporary.Path, "moved");
        Directory.CreateDirectory(childPath);

        using var root = BackupRestoreDirectory.OpenRoot(temporary.Path);
        using var child = root.OpenDirectory("child");

        Assert.ThrowsAny<IOException>(() => Directory.Move(childPath, movedPath));
        Assert.True(Directory.Exists(childPath));
        Assert.False(Directory.Exists(movedPath));
    }

    private static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "/c", "mklink", "/J", linkPath, targetPath }
            }) ?? throw new InvalidOperationException("Could not start cmd.exe to create a test junction.");
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
            return;
        }

        Directory.CreateSymbolicLink(linkPath, targetPath);
    }

    private static int CountOpenDirectoryHandles(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Directory.EnumerateFiles("/proc/self/fd")
            .Select(descriptor => new FileInfo(descriptor).LinkTarget)
            .Count(target => string.Equals(target, fullPath, StringComparison.Ordinal));
    }

    private static void SetLinuxDefaultAcl(string path, (ushort Tag, ushort Permissions, uint Id)[] entries)
    {
        const int openDirectory = 0x10000;
        const int openCloseOnExec = 0x80000;
        const int aclHeaderSize = sizeof(uint);
        const int aclEntrySize = 8;
        var descriptor = OpenLinux(path, openDirectory | openCloseOnExec);
        if (descriptor < 0)
        {
            throw new IOException("Could not open the Linux restore test directory.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        using var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        var acl = new byte[aclHeaderSize + (entries.Length * aclEntrySize)];
        BinaryPrimitives.WriteUInt32LittleEndian(acl, 2);
        for (var index = 0; index < entries.Length; index++)
        {
            var offset = aclHeaderSize + (index * aclEntrySize);
            BinaryPrimitives.WriteUInt16LittleEndian(acl.AsSpan(offset), entries[index].Tag);
            BinaryPrimitives.WriteUInt16LittleEndian(acl.AsSpan(offset + 2), entries[index].Permissions);
            BinaryPrimitives.WriteUInt32LittleEndian(acl.AsSpan(offset + 4), entries[index].Id);
        }

        if (SetFileXAttr(handle, "system.posix_acl_default", acl, (nuint)acl.Length, 0) != 0)
        {
            throw new IOException("Could not install a POSIX default ACL for the Linux restore test.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenLinux([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "mount", SetLastError = true)]
    private static extern int Mount(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string target,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? fileSystemType,
        ulong flags,
        IntPtr data);

    [DllImport("libc", EntryPoint = "umount2", SetLastError = true)]
    private static extern int Unmount([MarshalAs(UnmanagedType.LPUTF8Str)] string target, int flags);

    [DllImport("libc", EntryPoint = "fsetxattr", SetLastError = true)]
    private static extern int SetFileXAttr(SafeFileHandle descriptor, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, byte[] value, nuint size, int flags);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(profilePath))
            {
                throw new InvalidOperationException("Restore directory tests require a user profile for private filesystem fixtures.");
            }

            var fixtureRoot = System.IO.Path.Combine(profilePath, ".mulletaflix-restore-dir-tests");
            Directory.CreateDirectory(fixtureRoot);
            Path = System.IO.Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            DeleteTemporaryDirectory(Path);
        }

        private static void DeleteTemporaryDirectory(string directory)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    Directory.Delete(entry);
                }
                else if ((attributes & FileAttributes.Directory) != 0)
                {
                    DeleteTemporaryDirectory(entry);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            Directory.Delete(directory);
        }
    }
}
