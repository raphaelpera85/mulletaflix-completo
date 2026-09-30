using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations;
using Emby.Server.Implementations.Configuration;
using Emby.Server.Implementations.Serialization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Configuration;

public sealed class NamedConfigurationConcurrencyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mulletaflix-config-concurrency-" + Guid.NewGuid().ToString("N"));
    private readonly MyXmlSerializer _serializer = new();

    [Fact]
    public async Task ConcurrentUpdates_KeepAllChangesAndPersistTheSameStateAsTheCache()
    {
        var manager = CreateManager(_serializer);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = Enumerable.Range(0, 32).Select(async _ =>
        {
            await start.Task;
            manager.UpdateConfiguration("nebulaftp", current =>
            {
                var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
                next.SupabaseLastBackupFilesCount++;
                return next;
            });
        }).ToArray();

        start.SetResult();
        await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(30));

        var cached = manager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var disk = (NebulaFtpConfiguration)_serializer.DeserializeFromFile(typeof(NebulaFtpConfiguration), ConfigurationPath);
        Assert.Equal(32, cached.SupabaseLastBackupFilesCount);
        Assert.Equal(cached.SupabaseLastBackupFilesCount, disk.SupabaseLastBackupFilesCount);
    }

    [Fact]
    public async Task ResultUpdate_PreservesCurrentSettingsAndNotifiesOutsideTheGate()
    {
        var manager = CreateManager(_serializer);
        var original = manager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var settings = original.CreateSnapshot();
        settings.MaxWorkers = 3;
        manager.SaveConfiguration("nebulaftp", settings);
        var notifications = 0;
        manager.NamedConfigurationUpdated += (_, e) =>
        {
            var read = Task.Run(() => manager.GetConfiguration("nebulaftp"));
            Assert.True(read.Wait(TimeSpan.FromSeconds(5)), "Post-save notification must not hold the persistence gate.");
            Assert.Same(e.NewConfiguration, read.Result);
            notifications++;
        };

        await Task.Run(() => manager.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.SupabaseLastUsersBackupFailed = false;
            next.SupabaseLastUsersBackupCount = 8;
            next.SupabaseLastUsersBackupTime = DateTime.UnixEpoch;
            return next;
        })).WaitAsync(TimeSpan.FromSeconds(15));

        var cached = manager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        Assert.Equal(3, cached.MaxWorkers);
        Assert.Equal(8, cached.SupabaseLastUsersBackupCount);
        Assert.Null(original.SupabaseLastUsersBackupFailed);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void FailedSerialization_DoesNotPublishTheReplacementOrNotifySuccess()
    {
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(s => s.SerializeToFile(It.IsAny<NebulaFtpConfiguration>(), It.IsAny<string>()))
            .Throws(new IOException("test write failure"));
        var manager = CreateManager(serializer.Object);
        var original = manager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var notified = false;
        manager.NamedConfigurationUpdated += (_, _) => notified = true;

        Assert.Throws<IOException>(() => manager.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.SupabaseLastUsersBackupCount = 99;
            return next;
        }));

        Assert.Same(original, manager.GetConfiguration("nebulaftp"));
        Assert.Equal(0, original.SupabaseLastUsersBackupCount);
        Assert.False(notified);
    }

    [Fact]
    public void StaleOrdinarySave_DoesNotEraseNewerBackupHistory()
    {
        var manager = CreateManager(_serializer);
        var stale = manager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp").CreateSnapshot();
        manager.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.SupabaseLastBackupFailed = false;
            next.SupabaseLastBackupFilesCount = 42;
            next.SupabaseLastBackupAttemptTime = DateTime.UnixEpoch;
            return next;
        });
        stale.MaxWorkers = 3;

        manager.SaveConfiguration("nebulaftp", stale);

        var disk = (NebulaFtpConfiguration)_serializer.DeserializeFromFile(typeof(NebulaFtpConfiguration), ConfigurationPath);
        Assert.False(disk.SupabaseLastBackupFailed);
        Assert.Equal(42, disk.SupabaseLastBackupFilesCount);
        Assert.Equal(3, disk.MaxWorkers);
    }

    [Fact]
    public void ConfigurationKeyAliases_UseTheSameCurrentState()
    {
        var manager = CreateManager(_serializer);
        var stale = manager.GetConfiguration<NebulaFtpConfiguration>("NEBULAFTP").CreateSnapshot();
        manager.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.SupabaseLastBackupFailed = false;
            next.SupabaseLastBackupProcessedFilesCount = 42;
            return next;
        });

        Assert.Same(manager.GetConfiguration("nebulaftp"), manager.GetConfiguration("NEBULAFTP"));
        manager.SaveConfiguration("NEBULAFTP", stale);
        Assert.Equal(42, manager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp").SupabaseLastBackupProcessedFilesCount);
    }

    [Fact]
    public async Task ConcurrentResultWriters_PreserveSettingsAndIndependentOutcomeGroups()
    {
        var configuration = CreateManager(_serializer);
        var settings = configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp").CreateSnapshot();
        settings.MaxWorkers = 3;
        configuration.SaveConfiguration("nebulaftp", settings);
        await using var manager = new NebulaFtpManager(configuration, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Run(Action record)
        {
            await start.Task;
            record();
        }

        var backup = Run(() => manager.RecordMongoBackupResult(new NebulaSupabaseBackupResultDto { Success = true, FilesBackedUp = 42, UsersBackedUp = 7 }));
        var restore = Run(() => manager.RecordMongoRestoreResult(false, "test restore failure", 0, 0));
        var users = Task.Run(async () =>
        {
            await start.Task;
            await manager.BackupUsersToSupabaseAsync();
        });
        start.SetResult();
        await Task.WhenAll(backup, restore, users).WaitAsync(TimeSpan.FromSeconds(30));

        var disk = (NebulaFtpConfiguration)_serializer.DeserializeFromFile(typeof(NebulaFtpConfiguration), ConfigurationPath);
        Assert.Equal(3, disk.MaxWorkers);
        Assert.False(disk.SupabaseLastBackupFailed);
        Assert.Equal(42, disk.SupabaseLastBackupProcessedFilesCount);
        Assert.Equal(7, disk.SupabaseLastBackupProcessedUsersCount);
        Assert.True(disk.SupabaseLastUsersBackupFailed);
        Assert.True(disk.SupabaseLastRestoreFailed);
        Assert.Contains("test restore failure", disk.SupabaseLastRestoreStatus, StringComparison.Ordinal);
        var lastSuccess = disk.SupabaseLastBackupTime;
        manager.RecordMongoBackupResult(new NebulaSupabaseBackupResultDto { Success = false, Message = "test backup failure" });
        var afterFailure = configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        Assert.Equal(lastSuccess, afterFailure.SupabaseLastBackupTime);
        Assert.Null(afterFailure.SupabaseLastBackupProcessedFilesCount);
        Assert.True(afterFailure.SupabaseLastRestoreFailed);
    }

    [Fact]
    public void PartialSerializationFailure_PreservesDiskAndCacheAndRemovesTemporaryFile()
    {
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(s => s.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((value, path) => _serializer.SerializeToFile(value, path));
        var manager = CreateManager(serializer.Object);
        manager.UpdateConfiguration("nebulaftp", current => ((NebulaFtpConfiguration)current).CreateSnapshot());
        var before = File.ReadAllBytes(ConfigurationPath);
        var original = manager.GetConfiguration("nebulaftp");
        serializer.Setup(s => s.SerializeToFile(It.IsAny<NebulaFtpConfiguration>(), It.IsAny<string>()))
            .Callback<object, string>((_, path) =>
            {
                File.WriteAllText(path, "<partial");
                throw new IOException("test partial write failure");
            });

        Assert.Throws<IOException>(() => manager.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.MaxWorkers = 99;
            return next;
        }));

        Assert.Equal(before, File.ReadAllBytes(ConfigurationPath));
        Assert.Same(original, manager.GetConfiguration("nebulaftp"));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigurationPath)!, "*.tmp"));
    }

    [Fact]
    public void ThrowingSubscriber_DoesNotTurnCommittedSuccessIntoFailureOrSkipOtherSubscribers()
    {
        var manager = CreateManager(_serializer);
        manager.NamedConfigurationUpdated += (_, _) => throw new InvalidOperationException("test subscriber failure");
        var notified = false;
        manager.NamedConfigurationUpdated += (_, _) => notified = true;

        var replacement = manager.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.SupabaseLastBackupFailed = false;
            return next;
        });

        Assert.Same(replacement, manager.GetConfiguration("nebulaftp"));
        Assert.False(((NebulaFtpConfiguration)replacement).SupabaseLastBackupFailed);
        Assert.True(notified);
    }

    [Fact]
    public async Task NotificationWriters_PreserveIndependentConcurrentChangesAndOriginalSnapshot()
    {
        var configuration = CreateManager(_serializer);
        var original = (NebulaFtpConfiguration)configuration.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.PublicServerUrl = "https://example.org/old-path";
            next.Password = "test-password";
            next.MaxWorkers = 3;
            return next;
        });
        await using var manager = new NebulaFtpManager(configuration, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);
        using var commitEntered = new ManualResetEventSlim();
        using var releaseCommit = new ManualResetEventSlim();
        var firstCommit = true;
        configuration.NamedConfigurationUpdating += (_, _) =>
        {
            if (!firstCommit)
            {
                return;
            }
            firstCommit = false;
            commitEntered.Set();
            if (!releaseCommit.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("test commit was not released");
            }
        };
        var settings = Task.Run(() => configuration.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.PublicServerUrl = "https://current.example.org/new-path";
            next.Password = "rotated-test-password";
            next.MaxWorkers = 7;
            return next;
        }));
        Task<bool> notifications;
        Task<bool> telegram;
        Task backup;
        try
        {
            Assert.True(commitEntered.Wait(TimeSpan.FromSeconds(30)));
            var notificationsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var telegramStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            notifications = Task.Run(() =>
            {
                notificationsStarted.SetResult();
                return manager.SaveNotificationsSettings(new NebulaNotificationsSettingsRequest
                {
                    Enabled = true, IntervalSeconds = 100, ChannelIds = "-1001, -1001, invalid, -1002", PublicServerUrl = null
                });
            });
            telegram = Task.Run(() =>
            {
                telegramStarted.SetResult();
                return manager.SaveTelegramNotificationSettings(new NebulaTelegramNotificationSettingsRequest
                {
                    Enabled = true, IntervalSeconds = 0, ChatIds = "-2001, -2002"
                });
            });
            backup = Task.Run(() => manager.RecordMongoBackupResult(new NebulaSupabaseBackupResultDto { Success = true, FilesBackedUp = 42 }));
            await Task.WhenAll(notificationsStarted.Task, telegramStarted.Task).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(notifications.IsCompleted);
            Assert.False(telegram.IsCompleted);
        }
        finally
        {
            releaseCommit.Set();
        }

        await Task.WhenAll(settings, notifications, telegram, backup).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(await notifications);
        Assert.True(await telegram);
        var disk = (NebulaFtpConfiguration)_serializer.DeserializeFromFile(typeof(NebulaFtpConfiguration), ConfigurationPath);
        Assert.Equal("-1001,-1002", disk.NotificationsChannelIds);
        Assert.Equal(60, disk.NotificationsIntervalSeconds);
        Assert.True(disk.NotificationsEnabled);
        Assert.Equal("https://current.example.org", disk.PublicServerUrl);
        Assert.Equal("-2001,-2002", disk.TelegramNotificationChatIds);
        Assert.Equal("-2001", disk.ChatId);
        Assert.Equal(1, disk.TelegramNotificationIntervalSeconds);
        Assert.True(disk.TelegramNotificationsEnabled);
        Assert.Equal(42, disk.SupabaseLastBackupProcessedFilesCount);
        Assert.Equal("rotated-test-password", disk.Password);
        Assert.Equal(7, disk.MaxWorkers);
        Assert.Equal("https://example.org/old-path", original.PublicServerUrl);
        Assert.NotEqual("-1001,-1002", original.NotificationsChannelIds);
        Assert.NotEqual("-2001,-2002", original.TelegramNotificationChatIds);
        var cached = configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        Assert.Equal(disk.NotificationsChannelIds, cached.NotificationsChannelIds);
        Assert.Equal(disk.TelegramNotificationChatIds, cached.TelegramNotificationChatIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationSaveFailure_PreservesOriginalCacheAndDisk(bool telegram)
    {
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(s => s.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((value, path) => _serializer.SerializeToFile(value, path));
        var configuration = CreateManager(serializer.Object);
        configuration.UpdateConfiguration("nebulaftp", current => ((NebulaFtpConfiguration)current).CreateSnapshot());
        var original = configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var before = File.ReadAllBytes(ConfigurationPath);
        serializer.Setup(s => s.SerializeToFile(It.IsAny<NebulaFtpConfiguration>(), It.IsAny<string>()))
            .Throws(new IOException("test notification save failure"));
        await using var manager = new NebulaFtpManager(configuration, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);

        Assert.Throws<IOException>(() =>
        {
            if (telegram)
            {
                manager.SaveTelegramNotificationSettings(new NebulaTelegramNotificationSettingsRequest { Enabled = true, ChatIds = "-1001" });
            }
            else
            {
                manager.SaveNotificationsSettings(new NebulaNotificationsSettingsRequest { Enabled = true, ChannelIds = "-1001" });
            }
        });

        Assert.Same(original, configuration.GetConfiguration("nebulaftp"));
        Assert.NotEqual("-1001", original.NotificationsChannelIds);
        Assert.NotEqual("-1001", original.TelegramNotificationChatIds);
        Assert.Equal(before, File.ReadAllBytes(ConfigurationPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigurationPath)!, "*.tmp"));
    }

    [Fact]
    public void NoOpUpdate_DoesNotPersistOrNotify()
    {
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(s => s.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((value, path) => _serializer.SerializeToFile(value, path));
        var manager = CreateManager(serializer.Object);
        var original = manager.UpdateConfiguration("nebulaftp", current => ((NebulaFtpConfiguration)current).CreateSnapshot());
        var before = File.ReadAllBytes(ConfigurationPath);
        var notified = false;
        manager.NamedConfigurationUpdating += (_, _) => notified = true;
        manager.NamedConfigurationUpdated += (_, _) => notified = true;
        serializer.Setup(s => s.SerializeToFile(It.IsAny<NebulaFtpConfiguration>(), It.IsAny<string>()))
            .Throws(new IOException("no-op must not serialize"));

        var result = manager.UpdateConfiguration("nebulaftp", current => current);

        Assert.Same(original, result);
        Assert.Same(original, manager.GetConfiguration("nebulaftp"));
        Assert.False(notified);
        Assert.Equal(before, File.ReadAllBytes(ConfigurationPath));
    }

    [Fact]
    public async Task ConcurrentBotAdds_PreserveEveryTokenAndMaskResponses()
    {
        var configuration = CreateManager(_serializer);
        var original = configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        await using var manager = new NebulaFtpManager(configuration, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);
        var tokens = Enumerable.Range(1, 12).Select(i => $"test-bot-{i:0000}").ToArray();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = tokens.Select(token => Task.Run(async () =>
        {
            await start.Task;
            manager.SaveBot(new NebulaSaveBotRequest { Token = token });
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(30));

        var disk = (NebulaFtpConfiguration)_serializer.DeserializeFromFile(typeof(NebulaFtpConfiguration), ConfigurationPath);
        Assert.Equal(tokens.Order(StringComparer.Ordinal), disk.BotTokens.Split(',').Order(StringComparer.Ordinal));
        Assert.True(string.IsNullOrEmpty(original.BotTokens));
        manager.SaveBot(new NebulaSaveBotRequest { Token = " test-bot-0001 " });
        Assert.Equal(12, manager.GetBots().Count);
        Assert.All(manager.GetBots(), bot => Assert.Equal(string.Empty, bot.Token));
        Assert.Contains(manager.GetBots(), bot => bot.MaskedToken == "••••0001");
    }

    [Fact]
    public async Task BotReplacementAndDeletion_UseCurrentOrderAndInvalidDeleteIsNoOp()
    {
        var configuration = CreateManager(_serializer);
        configuration.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.BotTokens = "test-first,test-second";
            next.MaxWorkers = 7;
            return next;
        });
        await using var manager = new NebulaFtpManager(configuration, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);
        var original = configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        manager.SaveBot(new NebulaSaveBotRequest { Index = 2, Token = " test-replaced " });
        Assert.Equal("test-first,test-second", original.BotTokens);
        Assert.Equal("test-first,test-replaced", configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp").BotTokens);
        manager.DeleteBot(1);
        var afterDelete = configuration.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        Assert.Equal("test-replaced", afterDelete.BotTokens);
        Assert.Equal(7, afterDelete.MaxWorkers);
        var before = File.ReadAllBytes(ConfigurationPath);
        var notified = false;
        configuration.NamedConfigurationUpdated += (_, _) => notified = true;

        manager.DeleteBot(99);
        manager.DeleteBot(0);

        Assert.False(notified);
        Assert.Same(afterDelete, configuration.GetConfiguration("nebulaftp"));
        Assert.Equal(before, File.ReadAllBytes(ConfigurationPath));
    }

    [Theory]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("ftp")]
    public async Task CredentialWriterFailure_PreservesOriginalCacheAndDisk(string operation)
    {
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(s => s.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((value, path) => _serializer.SerializeToFile(value, path));
        var configuration = CreateManager(serializer.Object);
        var original = configuration.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.BotTokens = "test-original";
            next.Password = string.Empty;
            return next;
        });
        var before = File.ReadAllBytes(ConfigurationPath);
        serializer.Setup(s => s.SerializeToFile(It.IsAny<NebulaFtpConfiguration>(), It.IsAny<string>()))
            .Throws(new IOException("test credential write failure"));
        await using var manager = new NebulaFtpManager(configuration, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);

        Assert.Throws<IOException>(() =>
        {
            switch (operation)
            {
                case "add": manager.SaveBot(new NebulaSaveBotRequest { Token = "test-new" }); break;
                case "delete": manager.DeleteBot(1); break;
                default: manager.EnsureLocalFtpCredentials(); break;
            }
        });

        Assert.Same(original, configuration.GetConfiguration("nebulaftp"));
        Assert.Equal("test-original", ((NebulaFtpConfiguration)original).BotTokens);
        Assert.Equal(string.Empty, ((NebulaFtpConfiguration)original).Password);
        Assert.Equal(before, File.ReadAllBytes(ConfigurationPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigurationPath)!, "*.tmp"));
    }

    [Fact]
    public async Task ConcurrentLocalCredentialProvisioning_GeneratesOnceAndPreservesExistingCredentials()
    {
        var configuration = CreateManager(_serializer);
        var original = (NebulaFtpConfiguration)configuration.UpdateConfiguration("nebulaftp", current =>
        {
            var next = ((NebulaFtpConfiguration)current).CreateSnapshot();
            next.Username = " test-admin ";
            next.Password = string.Empty;
            next.MaxWorkers = 7;
            return next;
        });
        await using var manager = new NebulaFtpManager(configuration, NullLogger<NebulaFtpManager>.Instance, NullLoggerFactory.Instance);
        var writes = 0;
        configuration.NamedConfigurationUpdated += (_, _) => Interlocked.Increment(ref writes);
        var credentials = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => manager.EnsureLocalFtpCredentials())));

        Assert.Equal(1, writes);
        Assert.All(credentials, pair => Assert.Equal(credentials[0], pair));
        Assert.Equal("test-admin", credentials[0].Username);
        Assert.Equal(32, credentials[0].Password.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", credentials[0].Password);
        Assert.Equal(string.Empty, original.Password);
        var disk = (NebulaFtpConfiguration)_serializer.DeserializeFromFile(typeof(NebulaFtpConfiguration), ConfigurationPath);
        Assert.Equal(credentials[0].Password, disk.Password);
        Assert.Equal(7, disk.MaxWorkers);
        Assert.Equal(credentials[0], manager.EnsureLocalFtpCredentials());
        Assert.Equal(1, writes);
    }

    private string ConfigurationPath => Path.Combine(_directory, "config", "nebulaftp.xml");

    private ServerConfigurationManager CreateManager(IXmlSerializer serializer)
    {
        Directory.CreateDirectory(_directory);
        var paths = new ServerApplicationPaths(_directory, Path.Combine(_directory, "logs"), Path.Combine(_directory, "config"), Path.Combine(_directory, "cache"), Path.Combine(_directory, "web"));
        var manager = new ServerConfigurationManager(paths, NullLoggerFactory.Instance, serializer);
        manager.AddParts([new TestFactory()]);
        return manager;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class TestFactory : IConfigurationFactory
    {
        public IEnumerable<ConfigurationStore> GetConfigurations() =>
            [new() { Key = "nebulaftp", ConfigurationType = typeof(NebulaFtpConfiguration) }];
    }
}
