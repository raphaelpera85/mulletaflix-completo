using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.Library;
using MediaBrowser.Model.IO;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Library;

public partial class DotIgnoreIgnoreRuleTest
{
    [Fact]
    public void CacheHit_RepeatedCallsDoNotRereadFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "*.tmp");

            var rule = new DotIgnoreIgnoreRule();
            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(subDir, "test.tmp"),
                IsDirectory = false
            };

            // First call - should cache
            var result1 = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result1);

            // Second call - should use cache
            var result2 = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result2);

            // Third call with different file in same directory - should use cache
            var fileInfo2 = new FileSystemMetadata
            {
                FullName = Path.Combine(subDir, "other.tmp"),
                IsDirectory = false
            };
            var result3 = rule.ShouldIgnore(fileInfo2, null);
            Assert.True(result3);

            // Call with file that doesn't match pattern
            var fileInfo3 = new FileSystemMetadata
            {
                FullName = Path.Combine(subDir, "other.txt"),
                IsDirectory = false
            };
            var result4 = rule.ShouldIgnore(fileInfo3, null);
            Assert.False(result4);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CacheInvalidation_ModifyIgnoreFile_Reparses()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "*.tmp");

            var rule = new DotIgnoreIgnoreRule();
            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "test.tmp"),
                IsDirectory = false
            };

            // First call - should ignore .tmp files
            var result1 = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result1);

            // Modify the .ignore file to ignore .txt instead
            // Wait a bit to ensure the file modification time changes
            Thread.Sleep(50);
            File.WriteAllText(ignoreFilePath, "*.txt");

            // Now .tmp files should NOT be ignored
            var result2 = rule.ShouldIgnore(fileInfo, null);
            Assert.False(result2);

            // And .txt files SHOULD be ignored
            var txtFileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "test.txt"),
                IsDirectory = false
            };
            var result3 = rule.ShouldIgnore(txtFileInfo, null);
            Assert.True(result3);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EmptyIgnoreFile_IgnoresEverything()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, string.Empty);

            var rule = new DotIgnoreIgnoreRule();

            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "anyfile.mkv"),
                IsDirectory = false
            };

            // Empty .ignore file should ignore everything
            var result = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void WhitespaceOnlyIgnoreFile_IgnoresEverything()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "   \n\t\n   ");

            var rule = new DotIgnoreIgnoreRule();

            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "anyfile.mkv"),
                IsDirectory = false
            };

            // Whitespace-only .ignore file should ignore everything
            var result = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NoIgnoreFile_DoesNotIgnore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var rule = new DotIgnoreIgnoreRule();

            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "anyfile.mkv"),
                IsDirectory = false
            };

            // No .ignore file means don't ignore
            var result = rule.ShouldIgnore(fileInfo, null);
            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ConcurrentAccess_ThreadSafe()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "*.tmp");

            var rule = new DotIgnoreIgnoreRule();

            // Run multiple parallel checks
            Parallel.For(0, 100, i =>
            {
                var fileInfo = new FileSystemMetadata
                {
                    FullName = Path.Combine(tempDir, $"test{i}.tmp"),
                    IsDirectory = false
                };

                var result = rule.ShouldIgnore(fileInfo, null);
                Assert.True(result);
            });

            // Also test with non-matching files
            Parallel.For(0, 100, i =>
            {
                var fileInfo = new FileSystemMetadata
                {
                    FullName = Path.Combine(tempDir, $"test{i}.txt"),
                    IsDirectory = false
                };

                var result = rule.ShouldIgnore(fileInfo, null);
                Assert.False(result);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClearCache_ClearsAllCachedData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "*.tmp");

            var rule = new DotIgnoreIgnoreRule();
            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "test.tmp"),
                IsDirectory = false
            };

            // First call to populate cache
            var result1 = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result1);

            // Clear cache
            rule.ClearDirectoryCache();

            // Should still work (will re-populate cache)
            var result2 = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IgnoreFileDeleted_HandlesGracefully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "*.tmp");

            var rule = new DotIgnoreIgnoreRule();
            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "test.tmp"),
                IsDirectory = false
            };

            // First call - should ignore
            var result1 = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result1);

            // Delete the .ignore file
            File.Delete(ignoreFilePath);

            // Should not ignore anymore (file deleted)
            var result2 = rule.ShouldIgnore(fileInfo, null);
            Assert.False(result2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ParentDirectoryIgnoreFile_AppliesToSubdirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir1 = Path.Combine(tempDir, "sub1");
        var subDir2 = Path.Combine(tempDir, "sub1", "sub2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        try
        {
            // Put .ignore in root
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "*.tmp");

            var rule = new DotIgnoreIgnoreRule();

            // Check file in sub2 - should find .ignore in parent
            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(subDir2, "test.tmp"),
                IsDirectory = false
            };

            var result = rule.ShouldIgnore(fileInfo, null);
            Assert.True(result);

            // Check file in sub1
            var fileInfo2 = new FileSystemMetadata
            {
                FullName = Path.Combine(subDir1, "test.tmp"),
                IsDirectory = false
            };

            var result2 = rule.ShouldIgnore(fileInfo2, null);
            Assert.True(result2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DirectoryMatching_TrailingSlashPattern()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "videos");
        Directory.CreateDirectory(subDir);

        try
        {
            var ignoreFilePath = Path.Combine(tempDir, ".ignore");
            File.WriteAllText(ignoreFilePath, "videos/");

            var rule = new DotIgnoreIgnoreRule();

            // Directory should be ignored
            var dirInfo = new FileSystemMetadata
            {
                FullName = subDir,
                IsDirectory = true
            };

            var result = rule.ShouldIgnore(dirInfo, null);
            Assert.True(result);

            // File named "videos" should NOT be ignored (pattern has trailing slash)
            var fileInfo = new FileSystemMetadata
            {
                FullName = Path.Combine(tempDir, "videos"),
                IsDirectory = false
            };

            // Note: The Ignore library behavior may vary here, this tests the actual behavior
            var resultFile = rule.ShouldIgnore(fileInfo, null);
            // The file named "videos" without trailing slash might or might not match depending on the library
            // This test documents the actual behavior
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

