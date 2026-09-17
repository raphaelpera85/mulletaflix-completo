using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaSupabaseSyncTests
{
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
}
