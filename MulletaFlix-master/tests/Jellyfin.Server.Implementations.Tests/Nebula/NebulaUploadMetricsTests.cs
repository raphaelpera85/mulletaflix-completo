using System.Collections.Generic;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Model.Nebula;
using MongoDB.Bson;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Nebula;

public sealed class NebulaUploadMetricsTests
{
    [Fact]
    public void MapUploadFailureStageCounts_NormalizesAndCombinesUnknownStages()
    {
        var documents = new List<BsonDocument>
        {
            new() { { "_id", NebulaUploadFailureStages.TelegramTransfer }, { "count", 4 } },
            new() { { "_id", "unexpected-value-from-legacy-data" }, { "count", 2 } },
            new() { { "_id", NebulaUploadFailureStages.TelegramTransfer }, { "count", 1 } }
        };

        var result = NebulaMongoContext.MapUploadFailureStageCounts(documents);

        Assert.Collection(
            result,
            transfer =>
            {
                Assert.Equal(NebulaUploadFailureStages.TelegramTransfer, transfer.Stage);
                Assert.Equal(5, transfer.Count);
            },
            unknown =>
            {
                Assert.Equal(NebulaUploadFailureStages.Unknown, unknown.Stage);
                Assert.Equal(2, unknown.Count);
            });
    }

    [Theory]
    [InlineData(NebulaUploadFailureStages.TelegramAvailability, NebulaUploadFailureStages.TelegramAvailability)]
    [InlineData(NebulaUploadFailureStages.TelegramTransfer, NebulaUploadFailureStages.TelegramTransfer)]
    [InlineData(NebulaUploadFailureStages.UploadIntegrity, NebulaUploadFailureStages.UploadIntegrity)]
    [InlineData("title-or-path-from-untrusted-data", NebulaUploadFailureStages.Unknown)]
    public void NormalizeUploadFailureStage_UsesBoundedSafeValues(string stage, string expected)
    {
        Assert.Equal(expected, NebulaMongoContext.NormalizeUploadFailureStage(stage));
    }
}
