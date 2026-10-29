using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MulletaFlix.Testing;
using Xunit;

namespace MulletaFlix.Server.Integration.Tests;

public sealed class StartupBackupRecoveryTests
{
    [Fact]
    public async Task CreateHost_ReplaysInterruptedRestoreBeforeMigrationsAndCompletesStartup()
    {
        Assert.SkipUnless(!OperatingSystem.IsMacOS(), "Interrupted restore startup requires Windows or Linux.");
        MariaDbTestEnvironment.SkipUnlessConfigured();

        const string restoredContent = "restored-before-startup";
        var beforeStartupMigrationObserved = false;
        string? restoredFilePath = null;
        var factory = new MulletaFlixApplicationFactory
        {
            BeforeBackupRecovery = paths =>
            {
                Directory.CreateDirectory(paths.ConfigurationDirectoryPath);
                var archivePath = Path.Combine(paths.ProgramDataPath, "startup-recovery-test.zip");
                restoredFilePath = Path.Combine(paths.ConfigurationDirectoryPath, "startup-recovery-probe.txt");
                CreateBackupArchive(archivePath, restoredContent);

                var journal = new BackupRestoreRecoveryJournal(paths.ProgramDataPath);
                var operation = journal.CreateAsync(archivePath).GetAwaiter().GetResult();
                var targetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(restoredFilePath))));
                var sidecarPrefix = $".mfx-restore-{operation.OperationId}-{targetHash}";
                File.WriteAllText(restoredFilePath, "interrupted-content");
                File.Move(restoredFilePath, Path.Combine(paths.ConfigurationDirectoryPath, $"{sidecarPrefix}.bak"));
                File.WriteAllText(Path.Combine(paths.ConfigurationDirectoryPath, $"{sidecarPrefix}.tmp"), "partially-staged-content");
                File.WriteAllText(restoredFilePath, "partially-installed-content");
            },
            BeforeStartupMigration = paths =>
            {
                Assert.Equal(restoredContent, File.ReadAllText(restoredFilePath!));
                Assert.False(new BackupRestoreRecoveryJournal(paths.ProgramDataPath).JournalExists());
                beforeStartupMigrationObserved = true;
            }
        };

        await ExecuteAndDisposeAsync(
            async () =>
            {
                using var client = factory.CreateClient();
                var response = await client.GetAsync("/System/Info/Public", TestContext.Current.CancellationToken);

                response.EnsureSuccessStatusCode();
                Assert.True(beforeStartupMigrationObserved, "Backup recovery must complete before startup migrations begin.");
                Assert.Equal(restoredContent, File.ReadAllText(restoredFilePath!));
                Assert.True(await factory.TestPluginDatabaseExistsAsync(), "The server must initialize IntroSkipper in its isolated test schema.");
            },
            factory.Dispose);
    }

    [Fact]
    public async Task ExecuteAndDisposeAsync_PreservesOperationFailureWhenCleanupAlsoFails()
    {
        var operationFailure = new InvalidOperationException("simulated startup failure");
        var cleanupFailure = new InvalidOperationException("simulated fixture cleanup failure");

        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => ExecuteAndDisposeAsync(
                () => Task.FromException(operationFailure),
                () => throw cleanupFailure));

        Assert.Collection(
            aggregate.InnerExceptions,
            exception => Assert.Same(operationFailure, exception),
            exception => Assert.Same(cleanupFailure, exception));
    }

    private static async Task ExecuteAndDisposeAsync(Func<Task> operation, Action cleanup)
    {
        Exception? operationFailure = null;
        Exception? cleanupFailure = null;
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            operationFailure = exception;
        }

        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            cleanupFailure = exception;
        }

        if (operationFailure is not null && cleanupFailure is not null)
        {
            throw new AggregateException("The integration test and fixture cleanup both failed.", operationFailure, cleanupFailure);
        }

        if (operationFailure is not null)
        {
            ExceptionDispatchInfo.Capture(operationFailure).Throw();
        }

        if (cleanupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }

    private static void CreateBackupArchive(string archivePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        var manifest = new BackupManifest
        {
            ServerVersion = new Version(10, 0, 0),
            BackupEngineVersion = new Version(0, 2, 0),
            DateCreated = DateTimeOffset.UtcNow,
            DatabaseTables = Array.Empty<string>(),
            Options = new BackupOptions { Database = false, Metadata = false, Subtitles = false, Trickplay = false }
        };

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteEntry(archive, "manifest.json", JsonSerializer.Serialize(manifest));
        WriteEntry(archive, "Config/startup-recovery-probe.txt", content);
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open());
        writer.Write(content);
    }
}
