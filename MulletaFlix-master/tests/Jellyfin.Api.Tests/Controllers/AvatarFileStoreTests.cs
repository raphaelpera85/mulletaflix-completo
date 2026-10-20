using System;
using System.IO;
using System.Runtime.InteropServices;
using MulletaFlix.Plugin.GetAvatar.Services;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class AvatarFileStoreTests
{
    [Fact]
    public void CreateNew_StoresContentAndDoesNotOverwriteExistingFile()
    {
        using var context = new AvatarFileStoreTestContext();
        using var store = new AvatarFileStore(context.AvatarDirectory);
        using (var destination = store.CreateNew("avatar.png"))
        {
            destination.Write([1, 2, 3]);
        }

        using var source = store.TryOpenRead("avatar.png");
        Assert.NotNull(source);
        Assert.Equal([1, 2, 3], ReadAll(source));
        Assert.Throws<IOException>(() => store.CreateNew("avatar.png"));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(context.AvatarDirectory, "avatar.png")));
    }

    [Fact]
    public void TryOpenRead_RejectsPathComponents()
    {
        using var context = new AvatarFileStoreTestContext();
        using var store = new AvatarFileStore(context.AvatarDirectory);

        Assert.Null(store.TryOpenRead("..\\outside.png"));
        Assert.Null(store.TryOpenRead("subfolder/avatar.png"));
    }

    [Fact]
    public void TryDelete_WhenReadHandleIsOpen_DeletesCatalogEntryAfterReadCloses()
    {
        using var context = new AvatarFileStoreTestContext();
        using var store = new AvatarFileStore(context.AvatarDirectory);
        using (var destination = store.CreateNew("avatar.png"))
        {
            destination.Write([1, 2, 3]);
        }

        var source = store.TryOpenRead("avatar.png");
        Assert.NotNull(source);
        Assert.Equal(AvatarFileDeleteResult.Deleted, store.TryDelete("avatar.png"));
        source.Dispose();

        Assert.False(File.Exists(Path.Combine(context.AvatarDirectory, "avatar.png")));
    }

    [Fact]
    public void TryOpenReadAndTryDelete_RejectSymbolicLinkToOutsideFile()
    {
        using var context = new AvatarFileStoreTestContext();
        var outsideFile = Path.Combine(context.Root, "outside.png");
        var linkFile = Path.Combine(context.AvatarDirectory, "linked.png");
        File.WriteAllBytes(outsideFile, [7, 8, 9]);
        try
        {
            File.CreateSymbolicLink(linkFile, outsideFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {exception.GetType().Name}.");
        }

        using var store = new AvatarFileStore(context.AvatarDirectory);
        Assert.Null(store.TryOpenRead("linked.png"));
        Assert.Equal(AvatarFileDeleteResult.Unsafe, store.TryDelete("linked.png"));
        Assert.True(File.Exists(linkFile));
        Assert.Equal([7, 8, 9], File.ReadAllBytes(outsideFile));
    }

    [Fact]
    public void TryOpenRead_RejectsFifoBeforeOpeningItForDataAccess()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Linux O_PATH/procfs behavior is only available on Linux.");
        }

        using var context = new AvatarFileStoreTestContext();
        var fifoPath = Path.Combine(context.AvatarDirectory, "avatar.fifo");
        if (MkFifo(fifoPath, 0x180) != 0)
        {
            throw new IOException($"mkfifo failed with errno {Marshal.GetLastPInvokeError()}.");
        }

        using var store = new AvatarFileStore(context.AvatarDirectory);
        Assert.Null(store.TryOpenRead("avatar.fifo"));
        Assert.Equal(AvatarFileDeleteResult.Unsafe, store.TryDelete("avatar.fifo"));
        Assert.True(File.Exists(fifoPath));
    }

    [Fact]
    public void TryDelete_WhenNameChangesDuringPersistence_PreservesReplacementFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Linux relative-entry identity behavior is only available on Linux.");
        }

        using var context = new AvatarFileStoreTestContext();
        var avatarPath = Path.Combine(context.AvatarDirectory, "avatar.png");
        var movedOriginalPath = Path.Combine(context.Root, "original-avatar.png");
        File.WriteAllBytes(avatarPath, [1, 2, 3]);
        using var store = new AvatarFileStore(context.AvatarDirectory);

        var result = store.TryDelete("avatar.png", () =>
        {
            File.Move(avatarPath, movedOriginalPath);
            File.WriteAllBytes(avatarPath, [9, 8, 7]);
        });

        Assert.Equal(AvatarFileDeleteResult.Unsafe, result);
        Assert.Equal([9, 8, 7], File.ReadAllBytes(avatarPath));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(movedOriginalPath));
    }

    [Fact]
    public void Constructor_RejectsLinuxCatalogDirectoryWritableByOtherUsers()
    {
        if (OperatingSystem.IsLinux())
        {
            using var context = new AvatarFileStoreTestContext();
            File.SetUnixFileMode(
                context.AvatarDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupWrite);

            Assert.Throws<IOException>(() => new AvatarFileStore(context.AvatarDirectory));
            return;
        }

        Assert.Skip("Linux catalog permissions are only available on Linux.");
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MkFifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    private sealed class AvatarFileStoreTestContext : IDisposable
    {
        public AvatarFileStoreTestContext()
        {
            Root = Path.Combine(Path.GetTempPath(), "GetAvatarFileStore", Guid.NewGuid().ToString("N"));
            AvatarDirectory = Path.Combine(Root, "avatars");
            Directory.CreateDirectory(AvatarDirectory);
        }

        public string Root { get; }

        public string AvatarDirectory { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
