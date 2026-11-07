using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Emby.Server.Implementations.Playlists;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Playlists;

public class PlaylistManagerTests
{
    [Fact]
    public void DetermineAdjustedIndexMoveToFirstPositionNoPriorInAllList()
    {
        var priorIndexAllChildren = 0;
        var newIndex = 0;

        var adjustedIndex = PlaylistManager.DetermineAdjustedIndex(priorIndexAllChildren, newIndex);

        Assert.Equal(0, adjustedIndex);
    }

    [Fact]
    public void DetermineAdjustedIndexPriorInMiddleOfAllList()
    {
        var priorIndexAllChildren = 2;
        var newIndex = 0;

        var adjustedIndex = PlaylistManager.DetermineAdjustedIndex(priorIndexAllChildren, newIndex);

        Assert.Equal(1, adjustedIndex);
    }

    [Fact]
    public void DetermineAdjustedIndexMoveMiddleOfPlaylist()
    {
        var priorIndexAllChildren = 2;
        var newIndex = 1;

        var adjustedIndex = PlaylistManager.DetermineAdjustedIndex(priorIndexAllChildren, newIndex);

        Assert.Equal(3, adjustedIndex);
    }

    [Fact]
    public void PersistPlaylistFileReplacesContents()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".m3u");
        try
        {
            File.WriteAllText(path, "old playlist");
            PlaylistManager.PersistPlaylistFile(path, "new playlist");
            Assert.Equal("new playlist", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PersistPlaylistFileKeepsSymbolicLinkAndUpdatesTarget()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var targetPath = Path.Combine(directory, "target.m3u");
        var linkPath = Path.Combine(directory, "link.m3u");
        try
        {
            File.WriteAllText(targetPath, "old playlist");
            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
            }

            PlaylistManager.PersistPlaylistFile(linkPath, "new playlist");
            Assert.NotNull(new FileInfo(linkPath).LinkTarget);
            Assert.Equal("new playlist", File.ReadAllText(targetPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void PersistPlaylistFilePreservesUnixMode()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are not available on Windows.");

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".m3u");
        try
        {
            File.WriteAllText(path, "old playlist");
            const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            File.SetUnixFileMode(path, mode);

            PlaylistManager.PersistPlaylistFile(path, "new playlist");

            Assert.Equal(mode, File.GetUnixFileMode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void PersistPlaylistFilePreservesExistingWindowsAcl()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows ACLs are only available on Windows.");

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".m3u");
        var file = new FileInfo(path);
        try
        {
            File.WriteAllText(path, "old playlist");
            var security = file.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var worldSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new FileSystemAccessRule(worldSid, FileSystemRights.Read, AccessControlType.Allow));
            file.SetAccessControl(security);

            PlaylistManager.PersistPlaylistFile(path, "new playlist");

            var rules = file.GetAccessControl().GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>();
            Assert.Contains(rules, rule => rule.IdentityReference.Equals(worldSid)
                && (rule.FileSystemRights & FileSystemRights.Read) == FileSystemRights.Read
                && rule.AccessControlType == AccessControlType.Allow);
            Assert.Equal("new playlist", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PersistPlaylistFileKeepsExistingContentsWhenReplacementFails()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The locked-file replacement contract is Windows-specific.");

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".m3u");
        try
        {
            File.WriteAllText(path, "old playlist");
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() => PlaylistManager.PersistPlaylistFile(path, "new playlist"));
            }

            Assert.Equal("old playlist", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

