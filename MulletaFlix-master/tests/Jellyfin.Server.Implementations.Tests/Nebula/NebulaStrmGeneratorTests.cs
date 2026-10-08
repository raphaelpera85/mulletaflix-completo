using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Nebula;

public class NebulaStrmGeneratorTests
{
    [Fact]
    public async Task GenerateFilesAsync_ReportsPartialWriteFailureAfterContinuingOtherRoots()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"mulletraflix-strm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var validRoot = Path.Combine(tempRoot, "valid");
        var blockedRoot = Path.Combine(tempRoot, "blocked");
        Directory.CreateDirectory(validRoot);
        await File.WriteAllTextAsync(blockedRoot, "not a directory");

        try
        {
            var config = new NebulaFtpConfiguration
            {
                MonitorPaths = new[] { validRoot, blockedRoot },
                ServerHost = "127.0.0.1"
            };
            var files = new[]
            {
                new BsonDocument
                {
                    ["name"] = "Movie.mkv",
                    ["parent"] = BsonNull.Value
                }
            };

            var exception = await Assert.ThrowsAsync<IOException>(() =>
                NebulaStrmGenerator.GenerateFilesAsync(
                    config,
                    new Dictionary<string, string>(),
                    files,
                    NullLogger<NebulaStrmGenerator>.Instance));

            Assert.Contains("1 generated/updated; 1 failed", exception.Message, StringComparison.Ordinal);
            var generatedFiles = Directory.GetFiles(validRoot, "*.strm", SearchOption.AllDirectories);
            Assert.Single(generatedFiles);
            Assert.Contains("/stream?id=", await File.ReadAllTextAsync(generatedFiles[0]), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateFilesAsync_PropagatesCancellationInsteadOfReportingSuccess()
    {
        var config = new NebulaFtpConfiguration
        {
            MonitorPaths = new[] { Path.GetTempPath() },
            ServerHost = "127.0.0.1"
        };
        var files = new[]
        {
            new BsonDocument
            {
                ["name"] = "Movie.mkv",
                ["parent"] = BsonNull.Value
            }
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            NebulaStrmGenerator.GenerateFilesAsync(
                config,
                new Dictionary<string, string>(),
                files,
                NullLogger<NebulaStrmGenerator>.Instance,
                cancellationToken: cancellation.Token));
    }
}
