using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Model.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaUploadCheckpointJournalTests
{
    [Fact]
    public async Task PendingPartSurvivesRestartAndIsClearedByAcknowledgement()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "c008a2fa-03b6-4a31-9f53-d3ac590d3a30";
            var journal = new NebulaUploadCheckpointJournal(directory);
            await journal.MarkPartPendingAsync(fileUuid, 13, 1234, 2, 8, 1, new string('b', 64), CancellationToken.None);

            var restarted = new NebulaUploadCheckpointJournal(directory);
            var pending = await restarted.LoadSnapshotAsync(fileUuid, 13, 1234, 2, 8, CancellationToken.None);
            Assert.Contains(1, pending.PendingPartNumbers);
            Assert.Equal(new string('b', 64), pending.PendingPartSha256[1]);
            Assert.Empty(pending.Parts);

            await restarted.SavePartAsync(fileUuid, 13, 1234, 2, 8, Part(1, 5, "mtproto:11", 11), CancellationToken.None);
            var acknowledged = await restarted.LoadSnapshotAsync(fileUuid, 13, 1234, 2, 8, CancellationToken.None);
            Assert.Empty(acknowledged.PendingPartNumbers);
            Assert.Single(acknowledged.Parts);
            Assert.Equal("mtproto:11", acknowledged.Parts[0].TgFileId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReloadAfterMongoCheckpointFailure_ReusesAcknowledgedParts()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "2f95a3c0-b78c-4da0-a43b-d0ecbb0792b1";
            var writer = new NebulaUploadCheckpointJournal(directory);
            await writer.SavePartAsync(fileUuid, 13, 1234, 2, 8, Part(0, 8, "telegram-file-1", 10), CancellationToken.None);
            await writer.SavePartAsync(fileUuid, 13, 1234, 2, 8, Part(1, 5, "telegram-file-2", 11), CancellationToken.None);

            // A new instance represents a server restart after Mongo failed to
            // persist the acknowledged Telegram response.
            var restarted = new NebulaUploadCheckpointJournal(directory);
            var recovered = await restarted.LoadAsync(fileUuid, 13, 1234, 2, 8, CancellationToken.None);

            Assert.Equal(2, recovered.Count);
            Assert.Equal(new[] { "telegram-file-1", "telegram-file-2" }, recovered.Select(part => part.TgFileId));
            Assert.Equal(new long[] { 10, 11 }, recovered.Select(part => part.TgMessageId));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentJournalInstances_PreserveEveryAcknowledgedPart()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "ea961fe2-88cf-4d66-8134-97e74a99a382";
            const int totalParts = 32;
            var journals = new[]
            {
                new NebulaUploadCheckpointJournal(directory),
                new NebulaUploadCheckpointJournal(directory)
            };

            var intents = Enumerable.Range(0, totalParts)
                .Select(partNumber => journals[partNumber % journals.Length].MarkPartPendingAsync(
                    fileUuid,
                    totalParts,
                    1234,
                    totalParts,
                    1,
                    partNumber,
                    new string('a', 64),
                    CancellationToken.None));
            await Task.WhenAll(intents);

            var pending = await journals[0].LoadSnapshotAsync(
                fileUuid,
                totalParts,
                1234,
                totalParts,
                1,
                CancellationToken.None);
            Assert.Equal(totalParts, pending.PendingPartNumbers.Count);

            var writes = Enumerable.Range(0, totalParts)
                .Select(partNumber => journals[partNumber % journals.Length].SavePartAsync(
                    fileUuid,
                    totalParts,
                    1234,
                    totalParts,
                    1,
                    Part(partNumber, 1, $"telegram-file-{partNumber}", partNumber + 1),
                    CancellationToken.None));
            await Task.WhenAll(writes);

            var snapshot = await journals[0].LoadSnapshotAsync(
                fileUuid,
                totalParts,
                1234,
                totalParts,
                1,
                CancellationToken.None);

            Assert.Equal(totalParts, snapshot.Parts.Count);
            Assert.Empty(snapshot.PendingPartNumbers);
            Assert.Equal(Enumerable.Range(0, totalParts), snapshot.Parts.Select(part => part.PartNumber));
            Assert.Equal(totalParts, snapshot.Parts.Select(part => part.TgMessageId).Distinct().Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ChangedSourceRequiresNewGeneration()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "4ac8646e-4d71-460f-9ce4-8d0a04c8c423";
            var journal = new NebulaUploadCheckpointJournal(directory);
            await journal.SavePartAsync(fileUuid, 8, 1234, 1, 8, Part(0, 8, "telegram-file", 10), CancellationToken.None);

            var snapshot = await journal.LoadSnapshotAsync(fileUuid, 8, 9999, 1, 8, CancellationToken.None);
            Assert.True(snapshot.RequiresNewGeneration);
            Assert.Empty(snapshot.Parts);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SavePartAsync_RejectsInvalidTelegramAcknowledgement()
    {
        var directory = CreateTestDirectory();
        try
        {
            var journal = new NebulaUploadCheckpointJournal(directory);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                journal.SavePartAsync(
                    "6f63ba74-e8f3-48c1-9743-4e70b19790d8",
                    8,
                    1234,
                    1,
                    8,
                    Part(0, 8, string.Empty, 10),
                    CancellationToken.None));
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Delete_RemovesCompletedUploadCheckpoint()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "b2701142-5c38-46dc-80b9-57a89c100377";
            var journal = new NebulaUploadCheckpointJournal(directory);
            await journal.SavePartAsync(fileUuid, 8, 1234, 1, 8, Part(0, 8, "telegram-file", 10), CancellationToken.None);

            journal.Delete(fileUuid);

            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSnapshotAsync_LegacyCheckpointRequiresNewGeneration()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "b2701142-5c38-46dc-80b9-57a89c100377";
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "b27011425c3846dc80b957a89c100377.json"),
                "{\"FormatVersion\":1,\"FileUuid\":\"b2701142-5c38-46dc-80b9-57a89c100377\",\"SourceSize\":8,\"SourceLastWriteUtcTicks\":1234,\"Parts\":[],\"PendingPartNumbers\":[0]}");

            var snapshot = await new NebulaUploadCheckpointJournal(directory)
                .LoadSnapshotAsync(fileUuid, 8, 1234, 1, 8, CancellationToken.None);

            Assert.True(snapshot.RequiresNewGeneration);
            Assert.Empty(snapshot.Parts);
            Assert.Empty(snapshot.PendingPartNumbers);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSnapshotAsync_RejectsDuplicatePendingPartSha256PartNumbers()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "b2701142-5c38-46dc-80b9-57a89c100377";
            await WriteCheckpointAsync(
                directory,
                fileUuid,
                "[{\"PartNumber\":0,\"Sha256\":\"" + new string('a', 64) + "\"},{\"PartNumber\":0,\"Sha256\":\"" + new string('b', 64) + "\"}]");

            await Assert.ThrowsAsync<InvalidDataException>(() => new NebulaUploadCheckpointJournal(directory)
                .LoadSnapshotAsync(fileUuid, 8, 1234, 1, 8, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSnapshotAsync_RejectsNullPendingPartSha256Entry()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "b2701142-5c38-46dc-80b9-57a89c100377";
            await WriteCheckpointAsync(directory, fileUuid, "[null]");

            await Assert.ThrowsAsync<InvalidDataException>(() => new NebulaUploadCheckpointJournal(directory)
                .LoadSnapshotAsync(fileUuid, 8, 1234, 1, 8, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSnapshotAsync_RejectsDuplicateConfirmedPartsAndPreservesCheckpoint()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "b2701142-5c38-46dc-80b9-57a89c100377";
            const string parts = "[{\"part_number\":0,\"size\":8,\"source_sha256\":\"" +
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"tg_file_id\":\"telegram-file\",\"tg_message_id\":10,\"tg_chat_id\":1,\"bot_index\":0,\"status\":\"completed\"},{\"part_number\":0,\"size\":8,\"source_sha256\":\"" +
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"tg_file_id\":\"telegram-file\",\"tg_message_id\":10,\"tg_chat_id\":1,\"bot_index\":0,\"status\":\"completed\"}]";
            var checkpointPath = await WriteCheckpointAsync(directory, fileUuid, "[]", parts: parts);
            var originalCheckpoint = await File.ReadAllTextAsync(checkpointPath);

            await Assert.ThrowsAsync<InvalidDataException>(() => new NebulaUploadCheckpointJournal(directory)
                .LoadSnapshotAsync(fileUuid, 8, 1234, 1, 8, CancellationToken.None));

            Assert.True(File.Exists(checkpointPath));
            Assert.Equal(originalCheckpoint, await File.ReadAllTextAsync(checkpointPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSnapshotAsync_RejectsDuplicatePendingPartNumbersAndPreservesCheckpoint()
    {
        var directory = CreateTestDirectory();
        try
        {
            const string fileUuid = "b2701142-5c38-46dc-80b9-57a89c100377";
            var checkpointPath = await WriteCheckpointAsync(directory, fileUuid, "[]", pendingPartNumbers: "[0,0]");
            var originalCheckpoint = await File.ReadAllTextAsync(checkpointPath);

            await Assert.ThrowsAsync<InvalidDataException>(() => new NebulaUploadCheckpointJournal(directory)
                .LoadSnapshotAsync(fileUuid, 8, 1234, 1, 8, CancellationToken.None));

            Assert.True(File.Exists(checkpointPath));
            Assert.Equal(originalCheckpoint, await File.ReadAllTextAsync(checkpointPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<string> WriteCheckpointAsync(
        string directory,
        string fileUuid,
        string pendingPartSha256,
        string parts = "[]",
        string pendingPartNumbers = "[]")
    {
        Directory.CreateDirectory(directory);
        var fileName = Guid.Parse(fileUuid).ToString("N") + ".json";
        var json = "{\"FormatVersion\":2,\"FileUuid\":\"" + fileUuid + "\",\"SourceSize\":8,\"SourceLastWriteUtcTicks\":1234,\"Parts\":" + parts + ",\"PendingPartNumbers\":" + pendingPartNumbers + ",\"PendingPartSha256\":" + pendingPartSha256 + "}";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "NebulaUploadCheckpointTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static NebulaFilePart Part(int number, long size, string fileId, long messageId)
    {
        return new NebulaFilePart
        {
            PartNumber = number,
            Size = size,
            SourceSha256 = new string('a', 64),
            TgFileId = fileId,
            TgMessageId = messageId,
            TgChatId = -100123,
            BotIndex = 0,
            Status = "completed",
            UploadedAt = 1_790_000_000
        };
    }
}
