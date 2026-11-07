using System;
using System.IO;
using System.Text;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.MidiaStorageOnline;

public sealed class MidiaStorageOnlineAtomicFileTests
{
    [Fact]
    public void WriteAllText_ReplacesExistingFileAndCleansTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "sync_manifest.json");
        File.WriteAllText(path, "old manifest");

        try
        {
            MidiaStorageOnlineAtomicFile.WriteAllText(path, "new manifest");

            Assert.Equal("new manifest", File.ReadAllText(path));
            Assert.Empty(Directory.EnumerateFiles(directory, ".sync_manifest.json.*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteAllText_WhenDestinationIsLocked_PreservesExistingFile()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows sharing semantics are required to reject replacement of an open file.");

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "sync_manifest.json");
        File.WriteAllText(path, "old manifest");

        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var exception = Record.Exception(() => MidiaStorageOnlineAtomicFile.WriteAllText(path, "new manifest"));
                Assert.True(exception is IOException or UnauthorizedAccessException, exception?.ToString());
            }

            Assert.Equal("old manifest", File.ReadAllText(path));
            Assert.Empty(Directory.EnumerateFiles(directory, ".sync_manifest.json.*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteAtomically_WhenWritingFails_PreservesExistingFileAndCleansTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "sync_manifest.json");
        File.WriteAllText(path, "old manifest");

        try
        {
            var exception = Record.Exception(() => MidiaStorageOnlineAtomicFile.WriteAtomically(path, stream =>
            {
                stream.Write(Encoding.UTF8.GetBytes("partial manifest"));
                throw new IOException("simulated write failure");
            }));

            Assert.IsType<IOException>(exception);
            Assert.Equal("old manifest", File.ReadAllText(path));
            Assert.Empty(Directory.EnumerateFiles(directory, ".sync_manifest.json.*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteAtomically_WhenWritingAndCleanupFail_PreservesOriginalException()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "sync_manifest.json");
        File.WriteAllText(path, "old manifest");

        try
        {
            var exception = Record.Exception(() => MidiaStorageOnlineAtomicFile.WriteAtomically(path, stream =>
            {
                var temporaryPath = ((FileStream)stream).Name;
                stream.Write(Encoding.UTF8.GetBytes("partial manifest"));
                stream.Dispose();
                File.Move(temporaryPath, temporaryPath + ".retained");
                Directory.CreateDirectory(temporaryPath);
                throw new IOException("simulated write failure");
            }));

            Assert.IsType<IOException>(exception);
            Assert.Equal("simulated write failure", exception.Message);
            Assert.IsAssignableFrom<Exception>(exception.Data["TemporaryFileCleanupException"]);
            Assert.Equal("old manifest", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteAtomically_WhenPromotionAndCleanupFail_PreservesPromotionException()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "sync_manifest.json");
        File.WriteAllText(path, "old manifest");

        try
        {
            var exception = Record.Exception(() => MidiaStorageOnlineAtomicFile.WriteAtomically(path, stream =>
            {
                var temporaryPath = ((FileStream)stream).Name;
                stream.Write(Encoding.UTF8.GetBytes("new manifest"));
                stream.Dispose();
                File.Move(temporaryPath, temporaryPath + ".retained");
                Directory.CreateDirectory(temporaryPath);
            }));

            if (OperatingSystem.IsWindows())
            {
                Assert.IsType<FileNotFoundException>(exception);
            }
            else
            {
                Assert.IsAssignableFrom<IOException>(exception);
            }

            Assert.IsType<UnauthorizedAccessException>(exception.Data["TemporaryFileCleanupException"]);
            Assert.Equal("old manifest", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
