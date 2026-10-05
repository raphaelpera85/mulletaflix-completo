using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using MongoDB.Bson;
using Moq;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Contexts;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaSupabaseSyncTests
{
    private readonly ITestOutputHelper _output;

    public NebulaSupabaseSyncTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AutomaticMongoBackupDefaultsToHourlyForConfigurationAndStatus()
    {
        var configuration = new NebulaFtpConfiguration();
        var status = new NebulaSupabaseStatusDto();

        Assert.Equal(1, configuration.SupabaseAutoBackupIntervalHours);
        Assert.Equal(1, status.AutoBackupIntervalHours);
        Assert.Equal(1, NebulaFtpConfiguration.DefaultSupabaseAutoBackupIntervalHours);
    }

    [Fact]
    public void AutomaticUserBackupHasAnIndependentDailyStatus()
    {
        var configuration = new NebulaFtpConfiguration();
        var status = new NebulaSupabaseStatusDto();

        Assert.Equal(24, NebulaFtpConfiguration.DefaultSupabaseUsersBackupIntervalHours);
        Assert.Equal(24, status.AutoUsersBackupIntervalHours);
        Assert.Null(configuration.SupabaseLastUsersBackupTime);
        Assert.Equal("Aguardando primeiro backup automático", configuration.SupabaseLastUsersBackupStatus);
        Assert.Equal(0, configuration.SupabaseLastUsersBackupCount);
    }

    [Fact]
    public void DeltaSync_GeneratesCorrectMinObjectIdForTimestamp()
    {
        var targetTime = new DateTime(2026, 9, 17, 3, 30, 0, DateTimeKind.Utc);
        var minOid = ObjectId.GenerateNewId(targetTime);

        Assert.Equal(targetTime.ToUniversalTime(), minOid.CreationTime);
    }

    [Fact]
    public void DeltaSync_NewerObjectIdIsGreaterThanWatermark()
    {
        var baseTime = new DateTime(2026, 9, 17, 3, 30, 0, DateTimeKind.Utc);
        var baseOid = ObjectId.GenerateNewId(baseTime);

        var laterTime = baseTime.AddMinutes(5);
        var laterOid = ObjectId.GenerateNewId(laterTime);

        Assert.True(laterOid >= baseOid);
    }

    [Fact]
    public void DeltaRestore_FilterQueryUsesIso8601TimestampWithTolerance()
    {
        var localLatest = new DateTime(2026, 9, 17, 4, 0, 0, DateTimeKind.Utc);
        var toleranceMargin = localLatest.AddMinutes(-5);
        var isoTimestamp = toleranceMargin.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        Assert.Equal("2026-09-17T03:55:00Z", isoTimestamp);
    }

    [Fact]
    public void SupabaseSyncService_DefaultStateHasNullTimestamps()
    {
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance);
        Assert.Null(service.LastSuccessfulBackupTime);
        Assert.Null(service.LastSuccessfulRestoreTime);
    }

    [Fact]
    public void StartContinuousSync_DoesNotStartAnotherCycleWhilePreviousCallbackIsStillRunning()
    {
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var secondCallbackStarted = new ManualResetEventSlim();
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance);
        var callbackCount = 0;
        Action<NebulaSupabaseBackupResultDto> callback = _ =>
        {
            if (Interlocked.Increment(ref callbackCount) == 1)
            {
                callbackStarted.Set();
                releaseCallback.Wait();
            }
            else
            {
                secondCallbackStarted.Set();
            }
        };

        try
        {
            service.StartContinuousSync("https://supabase.invalid", "sb_secret_test", usersBackupCompleted: callback);
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(10)));

            service.StartContinuousSync("https://supabase.invalid", "sb_secret_test", usersBackupCompleted: callback);

            Assert.False(secondCallbackStarted.Wait(TimeSpan.FromMilliseconds(250)));
            Assert.Equal(1, Volatile.Read(ref callbackCount));
        }
        finally
        {
            releaseCallback.Set();
            service.StopContinuousSync();
        }
    }

    [Fact]
    public async Task UsersBackupFailsWhenRelationalSourceIsNotConfigured()
    {
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance);

        var result = await service.PerformUsersBackupAsync("https://supabase.invalid", "sb_secret_test");

        Assert.False(result.Success);
        Assert.Contains("origem relacional", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SupabaseOperations_ReportBoundedMetricsAndReleaseActiveGaugeOnFailure()
    {
        var measurements = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == NebulaSupabaseSyncService.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance);
        var result = await service.PerformBackupAsync("https://supabase.invalid", "sb_publishable_test", progressAction: null);
        var userBackupResult = await service.PerformUsersBackupAsync("https://supabase.invalid", "sb_secret_test");

        Assert.False(result.Success);
        Assert.False(userBackupResult.Success);
        var operations = measurements.Where(item => item.Name == "mulletaflix.supabase.operations").ToArray();
        Assert.Equal(2, operations.Length);
        Assert.All(operations, operation =>
        {
            Assert.Equal(1, operation.Value);
            Assert.Contains(operation.Tags, tag => tag.Key == "result" && Equals(tag.Value, "failure"));
        });
        Assert.Contains(operations, operation => operation.Tags.Any(tag => tag.Key == "operation" && Equals(tag.Value, "mongodb_delta_sync")));
        Assert.Contains(operations, operation => operation.Tags.Any(tag => tag.Key == "operation" && Equals(tag.Value, "backup_app_users")));
        Assert.Equal(2, measurements.Count(item => item.Name == "mulletaflix.supabase.operation.duration" && item.Value >= 0));
        Assert.Equal([1d, -1d, 1d, -1d], measurements
            .Where(item => item.Name == "mulletaflix.supabase.active_operations")
            .Select(item => item.Value)
            .ToArray());
        Assert.All(measurements, item => Assert.DoesNotContain(item.Tags, tag =>
            tag.Key.Contains("url", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("key", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task UsersRestoreFailsWhenRelationalSourceIsNotConfigured()
    {
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance);

        var result = await service.PerformUsersRestoreAsync("https://supabase.invalid", "sb_secret_test");

        Assert.False(result.Success);
        Assert.Contains("origem relacional", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UsersBackupFailsWhenRemoteTableIsMissingInsteadOfReportingSuccess()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var factory = new Mock<IDbContextFactory<UsersDbContext>>();
        factory.Setup(value => value.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new UsersDbContext(options, NullLogger<UsersDbContext>.Instance));
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"code\":\"PGRST205\",\"message\":\"table not found\"}")
        });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            factory.Object,
            handler);

        var result = await service.PerformUsersBackupAsync("https://supabase.invalid", "sb_secret_test");

        Assert.False(result.Success);
        Assert.Contains("tabela mulletaflix_users não existe", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task FtpUserBackupFailsWhenSupabaseRejectsTheBatch()
    {
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("temporary write failure")
        });
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance, null, handler);
        var users = new[]
        {
            new BsonDocument { ["_id"] = "ftp-user", ["password_hash"] = "hash" }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BackupNebulaUsersAsync("https://supabase.invalid", "sb_secret_test", users));

        Assert.Contains("HTTP 503", exception.Message, StringComparison.Ordinal);
        Assert.Contains("temporary write failure", exception.Message, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task FtpUserBackupReportsCountOnlyAfterSupabaseAcceptsTheBatch()
    {
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.Created));
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance, null, handler);
        var users = new[]
        {
            new BsonDocument { ["_id"] = "ftp-user", ["password_hash"] = "hash" }
        };

        var count = await service.BackupNebulaUsersAsync("https://supabase.invalid", "sb_secret_test", users);

        Assert.Equal(1, count);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task BackupHistoryFailsWhenSupabaseRejectsTheRecord()
    {
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("history write unavailable")
        });
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance, null, handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordBackupHistoryAsync("https://supabase.invalid", "sb_secret_test", new { status = "success" }));

        Assert.Contains("HTTP 503", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("history write unavailable", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task BackupHistoryAcceptsSuccessfulSupabaseResponse()
    {
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.Created));
        using var service = new NebulaSupabaseSyncService(null!, NullLogger<NebulaSupabaseSyncService>.Instance, null, handler);

        await service.RecordBackupHistoryAsync("https://supabase.invalid", "sb_secret_test", new { status = "success" });

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task UsersRestoreFailsWhenRemoteReadIsRejectedInsteadOfReportingSuccess()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var factory = new Mock<IDbContextFactory<UsersDbContext>>();
        factory.Setup(value => value.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new UsersDbContext(options, NullLogger<UsersDbContext>.Instance));
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"code\":\"PGRST205\",\"message\":\"table not found\"}")
        });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            factory.Object,
            handler);

        var result = await service.PerformUsersRestoreAsync("https://supabase.invalid", "sb_secret_test");

        Assert.False(result.Success);
        Assert.Contains("HTTP 404", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
    }

    [Fact]
    public async Task UsersRestoreWritesAndQueriesRecordsInAnIsolatedDatabase()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var factory = CreateUsersContextFactory(options);
        var restoredId = Guid.NewGuid();
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"[{{\"id\":\"{restoredId}\",\"username\":\"restored-user\",\"normalized_username\":\"RESTORED-USER\",\"password\":\"hash-value\",\"authentication_provider_id\":\"default\",\"password_reset_provider_id\":\"default\",\"permissions\":[],\"license\":null}}]")
        });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            factory,
            handler);

        var result = await service.PerformUsersRestoreAsync("https://supabase.invalid", "sb_secret_test");

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.UsersRestored);
        Assert.True(double.IsFinite(result.ElapsedSeconds));
        _output.WriteLine(
            "Restore com fixture HTTP/EF InMemory: restoredUsers={0}; restoredFiles={1}; elapsedSeconds={2:F3}",
            result.UsersRestored,
            result.FilesRestored,
            result.ElapsedSeconds);
        await using var verificationDb = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance);
        var restoredUser = await verificationDb.Users.SingleAsync(user => user.Id == restoredId);
        Assert.Equal("restored-user", restoredUser.Username);
        Assert.Equal("RESTORED-USER", restoredUser.NormalizedUsername);
        Assert.Equal("hash-value", restoredUser.Password);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
    }

    [Fact]
    public async Task FullRestore_WritesAndQueriesApplicationUserFromIsolatedSupabaseFixture()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var factory = CreateUsersContextFactory(options);
        var restoredId = Guid.NewGuid();
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"[{{\"id\":\"{restoredId:D}\",\"username\":\"full-restore-user\",\"normalized_username\":\"FULL-RESTORE-USER\",\"password\":\"fixture-hash\",\"phone_number\":\"5550100\",\"must_update_password\":true,\"authentication_provider_id\":\"default\",\"password_reset_provider_id\":\"default\",\"enable_local_password\":true,\"enable_user_preference_access\":true,\"permissions\":[],\"license\":null}}]")
            },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            factory,
            handler);

        var result = await service.PerformRestoreAsync(
            "https://supabase.invalid",
            "sb_secret_test",
            progressAction: null,
            forceFullRestore: true,
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.UsersRestored);
        Assert.Equal(0, result.FilesRestored);
        Assert.True(double.IsFinite(result.ElapsedSeconds));
        _output.WriteLine(
            "Restore com fixture HTTP/EF InMemory: restoredUsers={0}; restoredFiles={1}; elapsedSeconds={2:F3}",
            result.UsersRestored,
            result.FilesRestored,
            result.ElapsedSeconds);
        Assert.NotNull(service.LastSuccessfulRestoreTime);
        Assert.Equal([HttpMethod.Get, HttpMethod.Get, HttpMethod.Get, HttpMethod.Get], handler.Methods);

        await using var verificationDb = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance);
        var restoredUser = await verificationDb.Users.SingleAsync(
            user => user.Id == restoredId,
            TestContext.Current.CancellationToken);
        Assert.Equal("full-restore-user", restoredUser.Username);
        Assert.Equal("FULL-RESTORE-USER", restoredUser.NormalizedUsername);
        Assert.Equal("fixture-hash", restoredUser.Password);
        Assert.Equal("5550100", restoredUser.PhoneNumber);
        Assert.True(restoredUser.MustUpdatePassword);
        Assert.True(restoredUser.EnableLocalPassword);
        Assert.True(restoredUser.EnableUserPreferenceAccess);
    }

    [Fact]
    public async Task MongoRestoreFailsWhenFtpUserReadIsRejected()
    {
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("FTP user table unavailable")
            });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            CreateUsersContextFactory(new DbContextOptionsBuilder<UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options),
            handler);

        var result = await service.PerformRestoreAsync("https://supabase.invalid", "sb_secret_test", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("restauração de usuários FTP", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HTTP 503", result.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Get }, handler.Methods);
    }

    [Fact]
    public async Task MongoRestoreFailsWhenCatalogHasRowsButMongoContextIsUnavailable()
    {
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "[{\"id\":\"restore-file\",\"name\":\"title.mkv\",\"parent\":null,\"size\":10,\"status\":\"completed\",\"parts\":[],\"doc_data\":{\"_id\":\"restore-file\"}}]")
        });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            CreateUsersContextFactory(new DbContextOptionsBuilder<UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options),
            handler);

        var result = await service.PerformRestoreAsync(
            "https://supabase.invalid",
            "sb_secret_test",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("MongoDB não está disponível", result.Message, StringComparison.Ordinal);
        Assert.Null(result.FilesRestored);
        Assert.Null(result.UsersRestored);
        Assert.Null(result.FtpUsersRestored);
        Assert.Null(result.AppUsersRestored);
        Assert.Null(service.LastSuccessfulRestoreTime);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task MongoRestoreFailsWhenBotTokenReadIsRejected()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("bot token table unavailable")
            });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            CreateUsersContextFactory(options),
            handler);

        var result = await service.PerformRestoreAsync("https://supabase.invalid", "sb_secret_test", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("restauração de tokens de bot", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HTTP 503", result.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Get, HttpMethod.Get, HttpMethod.Get }, handler.Methods);
    }

    [Fact]
    public async Task MongoRestoreFailsWhenFtpUserResponseIsNotAJsonArray()
    {
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"unexpected\":true}")
            });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            CreateUsersContextFactory(new DbContextOptionsBuilder<UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options),
            handler);

        var result = await service.PerformRestoreAsync("https://supabase.invalid", "sb_secret_test", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("não é uma lista JSON", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.FilesRestored);
        Assert.Null(result.UsersRestored);
        Assert.Null(result.FtpUsersRestored);
        Assert.Null(result.AppUsersRestored);
        Assert.Null(service.LastSuccessfulRestoreTime);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Get }, handler.Methods);
    }

    [Fact]
    public async Task MongoRestoreRetainsFtpUserCountWhenApplicationUserPhaseFails()
    {
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("application users unavailable") });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            CreateUsersContextFactory(new DbContextOptionsBuilder<UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options),
            handler);

        var result = await service.PerformRestoreAsync("https://supabase.invalid", "sb_secret_test", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(0, result.FilesRestored);
        Assert.Equal(0, result.FtpUsersRestored);
        Assert.Null(result.AppUsersRestored);
        Assert.Null(result.UsersRestored);
    }

    [Fact]
    public async Task MongoRestoreFailsWhenBotTokenResponseIsNotAJsonArray()
    {
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"unexpected\":true}") });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            CreateUsersContextFactory(new DbContextOptionsBuilder<UsersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options),
            handler);

        var result = await service.PerformRestoreAsync("https://supabase.invalid", "sb_secret_test", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("não é uma lista JSON", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.FilesRestored);
        Assert.Equal(0, result.UsersRestored);
        Assert.Equal(0, result.FtpUsersRestored);
        Assert.Equal(0, result.AppUsersRestored);
        Assert.Null(service.LastSuccessfulRestoreTime);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Get, HttpMethod.Get, HttpMethod.Get }, handler.Methods);
    }

    [Fact]
    public async Task MongoRestoreSucceedsWhenRemoteUserAndTokenTablesAreEmpty()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            CreateUsersContextFactory(options),
            handler);

        var result = await service.PerformRestoreAsync("https://supabase.invalid", "sb_secret_test");

        Assert.True(result.Success, result.Message);
        Assert.Equal(0, result.UsersRestored);
        Assert.Equal(0, result.FtpUsersRestored);
        Assert.Equal(0, result.AppUsersRestored);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Get, HttpMethod.Get, HttpMethod.Get }, handler.Methods);
    }

    [Fact]
    public async Task UsersBackupFailsWhenRemoteUserReconciliationReadIsRejected()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using (var seed = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance))
        {
            seed.Users.Add(new User("local-user", "default", "default"));
            await seed.SaveChangesAsync();
        }

        var factory = CreateUsersContextFactory(options);
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("temporarily unavailable")
            });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            factory,
            handler);

        var result = await service.PerformUsersBackupAsync("https://supabase.invalid", "sb_secret_test");

        Assert.False(result.Success);
        Assert.Contains("listar usuários MulletaFlix", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Get }, handler.Methods);
    }

    [Fact]
    public async Task UsersBackupFailsWhenRemoteUserDeletionIsRejected()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using (var seed = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance))
        {
            seed.Users.Add(new User("local-user", "default", "default"));
            await seed.SaveChangesAsync();
        }

        var factory = CreateUsersContextFactory(options);
        var staleRemoteId = Guid.NewGuid();
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[{{\"id\":\"{staleRemoteId}\"}}]")
            },
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("delete forbidden")
            });
        using var service = new NebulaSupabaseSyncService(
            null!,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            factory,
            handler);

        var result = await service.PerformUsersBackupAsync("https://supabase.invalid", "sb_secret_test");

        Assert.False(result.Success);
        Assert.Contains("remover registro antigo", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HTTP 403", result.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Get, HttpMethod.Delete }, handler.Methods);
    }

    private static IDbContextFactory<UsersDbContext> CreateUsersContextFactory(DbContextOptions<UsersDbContext> options)
    {
        var factory = new Mock<IDbContextFactory<UsersDbContext>>();
        factory.Setup(value => value.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new UsersDbContext(options, NullLogger<UsersDbContext>.Instance));
        return factory.Object;
    }

    private sealed class StaticResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastMethod = request.Method;
            return Task.FromResult(response);
        }
    }

    private sealed class SequenceResponseHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _nextResponse;

        public List<HttpMethod> Methods { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            return Task.FromResult(responses[_nextResponse++]);
        }
    }
}
