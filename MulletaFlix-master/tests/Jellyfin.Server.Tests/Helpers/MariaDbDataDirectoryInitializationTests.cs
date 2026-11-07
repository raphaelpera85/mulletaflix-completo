using System;
using System.IO;
using MulletaFlix.Server.Helpers;
using Xunit;

namespace MulletaFlix.Server.Tests.Helpers;

public class MariaDbDataDirectoryInitializationTests
{
    [Fact]
    public void RequiresDataDirectoryInitialization_ReturnsTrueForMissingOrEmptyDirectory()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            Assert.True(MariaDbProcessManager.RequiresDataDirectoryInitialization(Path.Combine(root, "missing")));
            Assert.True(MariaDbProcessManager.RequiresDataDirectoryInitialization(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureDataDirectoryInitializerExists_ThrowsWhenInitializerIsMissing()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(
                () => MariaDbProcessManager.EnsureDataDirectoryInitializerExists(Path.Combine(root, "missing-initializer.exe")));

            Assert.Contains("refusing to start with an uninitialized data directory", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureDataDirectoryInitializerExists_AllowsExistingInitializer()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var initializerPath = Path.Combine(root, "mysql_install_db.exe");
            File.WriteAllText(initializerPath, "test");

            MariaDbProcessManager.EnsureDataDirectoryInitializerExists(initializerPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RequiresDataDirectoryInitialization_ReturnsFalseWhenDirectoryHasFiles()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "mysql"));
            File.WriteAllText(Path.Combine(root, "ibdata1"), "test");

            Assert.False(MariaDbProcessManager.RequiresDataDirectoryInitialization(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mfx-mariadb-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
