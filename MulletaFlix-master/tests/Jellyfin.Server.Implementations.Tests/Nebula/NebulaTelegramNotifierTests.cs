using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaTelegramNotifierTests
{
    [Fact]
    public void QueueMetrics_ReportDepthAndBoundedOutcomesWithoutSensitiveDimensions()
    {
        long acceptedMetadataChecks = 0;
        long rejectedMetadataChecks = 0;
        long acceptedNotifications = 0;
        long rejectedNotifications = 0;
        long pendingMetadata = -1;
        long pendingNotifications = -1;
        long activeWorkers = -1;
        var resultTags = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == NotificationsLibraryNotifier.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name is "mulletaflix.nebula.notifications.metadata.checks"
                or "mulletaflix.nebula.notifications.delivery.queued")
            {
                Assert.Equal(1, tags.Length);
                Assert.Equal("result", tags[0].Key);
                var result = tags[0].Value?.ToString();
                if (instrument.Name == "mulletaflix.nebula.notifications.metadata.checks" && result == "accepted")
                {
                    acceptedMetadataChecks += measurement;
                }
                else if (instrument.Name == "mulletaflix.nebula.notifications.metadata.checks")
                {
                    rejectedMetadataChecks += measurement;
                }
                else if (result == "accepted")
                {
                    acceptedNotifications += measurement;
                }
                else
                {
                    rejectedNotifications += measurement;
                }

                resultTags.Add(result ?? string.Empty);
            }
            else if (instrument.Name == "mulletaflix.nebula.notifications.metadata.pending")
            {
                pendingMetadata = measurement;
            }
            else if (instrument.Name == "mulletaflix.nebula.notifications.delivery.pending")
            {
                pendingNotifications = measurement;
            }
            else if (instrument.Name == "mulletaflix.nebula.notifications.metadata.workers.active")
            {
                activeWorkers = measurement;
            }
            else
            {
                Assert.True(tags.IsEmpty);
            }
        });
        listener.Start();

        using var notifier = CreateNotifier();
        for (var index = 0; index < NotificationsLibraryNotifier.MaxPendingMetadataChecks; index++)
        {
            Assert.True(notifier.ScheduleMetadataCheck(Guid.NewGuid()));
        }

        Assert.False(notifier.ScheduleMetadataCheck(Guid.NewGuid()));
        for (var index = 0; index < NotificationsLibraryNotifier.MaxPendingNotifications; index++)
        {
            Assert.True(notifier.TryQueueNotification(Guid.NewGuid(), "private title", "private/path.jpg"));
        }

        Assert.False(notifier.TryQueueNotification(Guid.NewGuid(), "overflow", "private/path.jpg"));
        listener.RecordObservableInstruments();

        Assert.Equal(NotificationsLibraryNotifier.MaxPendingMetadataChecks, acceptedMetadataChecks);
        Assert.Equal(1, rejectedMetadataChecks);
        Assert.Equal(NotificationsLibraryNotifier.MaxPendingNotifications, acceptedNotifications);
        Assert.Equal(1, rejectedNotifications);
        Assert.Contains("accepted", resultTags);
        Assert.Contains("rejected", resultTags);
        Assert.Equal(NotificationsLibraryNotifier.MaxPendingMetadataChecks, pendingMetadata);
        Assert.Equal(NotificationsLibraryNotifier.MaxPendingNotifications, pendingNotifications);
        Assert.Equal(0, activeWorkers);
    }

    [Fact]
    public void MetadataCheckQueue_RejectsWorkAfterBoundedCapacity()
    {
        using var notifier = CreateNotifier();

        for (var index = 0; index < NotificationsLibraryNotifier.MaxPendingMetadataChecks; index++)
        {
            Assert.True(notifier.ScheduleMetadataCheck(Guid.NewGuid()));
        }

        Assert.False(notifier.ScheduleMetadataCheck(Guid.NewGuid()));
        Assert.Equal(NotificationsLibraryNotifier.MaxPendingMetadataChecks, notifier.PendingMetadataCheckCount);
        Assert.Equal(NotificationsLibraryNotifier.MaxPendingMetadataChecks, notifier.ScheduledMetadataCheckCount);
    }

    [Fact]
    public void NotificationQueue_RejectsWorkAfterBoundedCapacity()
    {
        using var notifier = CreateNotifier();

        for (var index = 0; index < NotificationsLibraryNotifier.MaxPendingNotifications; index++)
        {
            Assert.True(notifier.TryQueueNotification(Guid.NewGuid(), "notification", null));
        }

        Assert.False(notifier.TryQueueNotification(Guid.NewGuid(), "overflow", null));
        Assert.Equal(NotificationsLibraryNotifier.MaxPendingNotifications, notifier.PendingNotificationCount);
    }

    [Fact]
    public async Task StopAsync_CancelsMetadataWorkersAndReleasesScheduledEntries()
    {
        using var notifier = CreateNotifier();
        await notifier.StartAsync(CancellationToken.None);
        Assert.True(notifier.ScheduleMetadataCheck(Guid.NewGuid()));

        await notifier.StopAsync(CancellationToken.None);

        Assert.Equal(0, notifier.ScheduledMetadataCheckCount);
        Assert.Equal(0, notifier.PendingMetadataCheckCount);
    }

    [Fact]
    public void IsCompletedTelegramMedia_RequiresContiguousPartsAndMatchingSize()
    {
        var complete = new BsonDocument
        {
            { "status", "completed" },
            { "size", 10L },
            {
                "parts",
                new BsonArray
                {
                    new BsonDocument { { "part_number", 0 }, { "size", 4L }, { "tg_file_id", "part-0" } },
                    new BsonDocument { { "part_number", 1 }, { "size", 6L }, { "tg_file_id", "part-1" } }
                }
            }
        };
        var incomplete = complete.DeepClone().AsBsonDocument;
        incomplete["parts"].AsBsonArray.RemoveAt(1);

        Assert.True(NebulaMongoContext.IsCompletedTelegramMedia(complete));
        Assert.False(NebulaMongoContext.IsCompletedTelegramMedia(incomplete));
    }

    [Fact]
    public void IsCompletedTelegramMedia_RejectsFailedPartAndNonCompletedDocument()
    {
        var document = new BsonDocument
        {
            { "status", "completed" },
            { "size", 10L },
            {
                "parts",
                new BsonArray
                {
                    new BsonDocument { { "part_number", 0 }, { "size", 10L }, { "tg_file_id", "part-0" }, { "status", "failed" } }
                }
            }
        };

        Assert.False(NebulaMongoContext.IsCompletedTelegramMedia(document));
        document["parts"].AsBsonArray[0].AsBsonDocument["status"] = "completed";
        document["status"] = "uploading";
        Assert.False(NebulaMongoContext.IsCompletedTelegramMedia(document));
    }

    [Fact]
    public void IsCompletedTelegramMedia_AcceptsLegacyRootIdButRejectsEmptyPartsArray()
    {
        var legacy = new BsonDocument
        {
            { "status", "completed" },
            { "size", 10L },
            { "tg_file_id", "legacy-file-id" }
        };
        var emptyParts = legacy.DeepClone().AsBsonDocument;
        emptyParts.Add("parts", new BsonArray());

        Assert.True(NebulaMongoContext.IsCompletedTelegramMedia(legacy));
        Assert.False(NebulaMongoContext.IsCompletedTelegramMedia(emptyParts));
    }

    [Fact]
    public void EpisodeIdentity_PreventsMovieIdentityFromCollapsingEpisodes()
    {
        var episode = NebulaDownloaderEngine.EpisodeIdentity(
            "Rebelde (2022)",
            "Rebelde (2022) - S01E02.strm");

        Assert.True(episode.HasValue);
        Assert.Equal("rebelde 2022", episode.Value.Series);
        Assert.Equal(1, episode.Value.Season);
        Assert.Equal(2, episode.Value.Episode);
    }

    [Fact]
    public void BuildDirectWebUrl_UsesPublicServerAndNeverLocalhost()
    {
        var itemId = Guid.Parse("e7b83cbe-1f78-2b32-9a24-90a40252e46a");
        var url = NotificationsLibraryNotifier.BuildDirectWebUrl(itemId, "http://mulletaflix.duckdns.org:8096/", "29ebbf0c786245469ecb2cccbb257f60");

        Assert.Equal("http://mulletaflix.duckdns.org:8096/web/#/details?id=e7b83cbe1f782b329a2490a40252e46a&serverId=29ebbf0c786245469ecb2cccbb257f60", url);
        Assert.DoesNotContain("localhost", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenNoBotTokensConfigured()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new List<string>(),
            -100123456789,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync("<b>Teste</b>", null, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenMessageIsEmpty()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new[] { "bot123:token" },
            -100123456789,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync(string.Empty, null, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenChatIdIsZeroOrEmpty()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new[] { "bot123:token" },
            0,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync("<b>Teste</b>", string.Empty, CancellationToken.None);
        Assert.False(result);
    }

    private static NotificationsLibraryNotifier CreateNotifier()
        => new(
            Mock.Of<ILibraryManager>(),
            Mock.Of<INebulaFtpManager>(),
            Mock.Of<IServerApplicationHost>(),
            NullLogger<NotificationsLibraryNotifier>.Instance);
}
