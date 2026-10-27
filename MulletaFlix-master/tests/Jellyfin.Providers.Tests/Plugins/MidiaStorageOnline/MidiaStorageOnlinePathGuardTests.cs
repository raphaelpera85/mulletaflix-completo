using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.MidiaStorageOnline;

public sealed class MidiaStorageOnlinePathGuardTests
{
    [Theory]
    [InlineData("Filmes/Title (2025)/Title.strm")]
    [InlineData("Series\\Show\\Season 01\\Episode.strm")]
    public void ResolveUnderRoot_ResolvesRelativeMediaPath(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "mfx-media-path-guard", Guid.NewGuid().ToString("N"));

        var resolved = MidiaStorageOnlinePathGuard.ResolveUnderRoot(root, relativePath);

        Assert.True(MidiaStorageOnlinePathGuard.IsPathWithinRoot(root, resolved));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("Filmes/../../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("Series\\Show\\..\\..\\outside.strm")]
    [InlineData("/outside.txt")]
    [InlineData("\\\\server\\share\\outside.txt")]
    [InlineData("C:outside.txt")]
    [InlineData("Filmes//Title.strm")]
    [InlineData("Filmes/./Title.strm")]
    public void ResolveUnderRoot_RejectsTraversalAndRootedPaths(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "mfx-media-path-guard", Guid.NewGuid().ToString("N"));

        Assert.Throws<InvalidDataException>(() => MidiaStorageOnlinePathGuard.ResolveUnderRoot(root, relativePath));
    }

    [Fact]
    public void EnsureNoReparsePoints_RejectsExistingSymbolicLink()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"mfx-media-path-guard-{Guid.NewGuid():N}");
        var root = Path.Combine(basePath, "root");
        var outside = Path.Combine(basePath, "outside");
        var link = Path.Combine(root, "linked");

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            Directory.CreateSymbolicLink(link, outside);
            var destination = Path.Combine(link, "movie.strm");

            Assert.Throws<InvalidDataException>(() => MidiaStorageOnlinePathGuard.EnsureNoReparsePoints(root, destination));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureNoReparsePoints_RejectsDanglingSymbolicLink()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"mfx-media-path-guard-{Guid.NewGuid():N}");
        var root = Path.Combine(basePath, "root");
        var missingTarget = Path.Combine(basePath, "missing-target");
        var link = Path.Combine(root, "linked");

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateSymbolicLink(link, missingTarget);
            var destination = Path.Combine(link, "movie.strm");

            Assert.Throws<InvalidDataException>(() => MidiaStorageOnlinePathGuard.EnsureNoReparsePoints(root, destination));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureNoReparsePoints_RejectsSymbolicLinkUsedAsOutputRoot()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"mfx-media-path-guard-{Guid.NewGuid():N}");
        var realRoot = Path.Combine(basePath, "real-root");
        var linkedRoot = Path.Combine(basePath, "linked-root");

        try
        {
            Directory.CreateDirectory(realRoot);
            Directory.CreateSymbolicLink(linkedRoot, realRoot);
            var destination = Path.Combine(linkedRoot, "Filmes", "movie.strm");

            Assert.Throws<InvalidDataException>(() => MidiaStorageOnlinePathGuard.EnsureNoReparsePoints(linkedRoot, destination));
        }
        finally
        {
            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteTextAtomicallyAsync_ReplacesHardLinkWithoutOverwritingItsTarget()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"mfx-media-path-guard-{Guid.NewGuid():N}");
        var root = Path.Combine(basePath, "root");
        var outside = Path.Combine(basePath, "outside.txt");
        var destination = Path.Combine(root, "movie.strm");

        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(outside, "outside-original", TestContext.Current.CancellationToken);
            var created = OperatingSystem.IsWindows()
                ? NativeMethods.CreateHardLinkWindows(destination, outside, IntPtr.Zero)
                : NativeMethods.CreateHardLinkUnix(outside, destination) == 0;
            Assert.True(created, "The test filesystem must support hard links.");

            await MidiaStorageOnlinePathGuard.WriteTextAtomicallyAsync(root, destination, "https://media.example/movie", TestContext.Current.CancellationToken);

            Assert.Equal("outside-original", await File.ReadAllTextAsync(outside, TestContext.Current.CancellationToken));
            Assert.Equal("https://media.example/movie", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(basePath))
            {
                Directory.Delete(basePath, recursive: true);
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateHardLinkWindows(string newFileName, string existingFileName, IntPtr securityAttributes);

        [DllImport("libc", EntryPoint = "link", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern int CreateHardLinkUnix(string existingFileName, string newFileName);
    }
}
