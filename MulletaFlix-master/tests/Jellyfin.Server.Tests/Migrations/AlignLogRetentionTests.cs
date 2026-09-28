using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MediaBrowser.Common.Configuration;
using Moq;
using MulletaFlix.Server.Migrations.Routines;
using Serilog;
using Xunit;

namespace MulletaFlix.Server.Tests.Migrations;

public class AlignLogRetentionTests
{
    [Fact]
    public void SerilogFileSink_NullCountLimit_RetainsMoreThanThreeRolls()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mulletaflix-serilog-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var logPath = Path.Combine(root, "log_.log");
            var json = """
                {"Serilog":{"MinimumLevel":"Information","WriteTo":[{"Name":"File","Args":{"path":__LOG_PATH__,"fileSizeLimitBytes":1,"rollingInterval":"Day","rollOnFileSizeLimit":true,"retainedFileCountLimit":null}}]}}
                """.Replace("__LOG_PATH__", System.Text.Json.JsonSerializer.Serialize(logPath), StringComparison.Ordinal);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
            using (var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger())
            {
                for (var i = 0; i < 40; i++)
                {
                    logger.Information("Retention verification event {Index}", i);
                }
            }

            Assert.Equal(40, Directory.GetFiles(root, "*.log").Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PerformAsync_RemovesBundledFileSinkCountLimitAndPreservesUserOverride()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mulletaflix-log-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var defaultPath = Path.Combine(root, "logging.default.json");
            var overridePath = Path.Combine(root, "logging.json");
            await File.WriteAllTextAsync(
                defaultPath,
                """
                    {"Serilog":{"WriteTo":[{"Name":"Async","Args":{"configure":[{"Name":"File","Args":{"retainedFileCountLimit":3,"path":"log_.log"}}]}}]}}
                    """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                overridePath,
                """
                    {"Serilog":{"WriteTo":[{"Name":"File","Args":{"retainedFileCountLimit":5}}]}}
                    """,
                TestContext.Current.CancellationToken);

            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(x => x.ConfigurationDirectoryPath).Returns(root);
            var migration = new AlignLogRetention(paths.Object);
            await migration.PerformAsync(TestContext.Current.CancellationToken);

            var updatedDefault = await File.ReadAllTextAsync(defaultPath, TestContext.Current.CancellationToken);
            var unchangedOverride = await File.ReadAllTextAsync(overridePath, TestContext.Current.CancellationToken);
            Assert.Contains("\"retainedFileCountLimit\": null", updatedDefault, StringComparison.Ordinal);
            Assert.Contains("\"retainedFileCountLimit\":5", unchangedOverride, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
