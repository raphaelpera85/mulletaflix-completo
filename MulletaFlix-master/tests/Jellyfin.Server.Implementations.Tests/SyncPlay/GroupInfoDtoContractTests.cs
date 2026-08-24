using System;
using MediaBrowser.Model.SyncPlay;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.SyncPlay;

/// <summary>
/// Contract tests for the SyncPlay <see cref="GroupInfoDto"/> payload exposed to clients.
/// </summary>
public class GroupInfoDtoContractTests
{
    [Fact]
    public void GroupInfoDto_ExposesHostAndPingForClients()
    {
        var groupId = Guid.NewGuid();
        var dto = new GroupInfoDto(groupId, "Movie Night", GroupStateType.Waiting, new[] { "alice", "bob" }, DateTime.UtcNow)
        {
            Ping = 123,
            Host = "alice"
        };

        Assert.Equal(groupId, dto.GroupId);
        Assert.Equal("Movie Night", dto.GroupName);
        Assert.Equal(GroupStateType.Waiting, dto.State);
        Assert.Equal(new[] { "alice", "bob" }, dto.Participants);
        Assert.Equal(123, dto.Ping);
        Assert.Equal("alice", dto.Host);
    }

    [Fact]
    public void GroupInfoDto_HostDefaultsToNull_WhenEmpty()
    {
        var dto = new GroupInfoDto(Guid.NewGuid(), "Empty", GroupStateType.Idle, Array.Empty<string>(), DateTime.UtcNow);

        Assert.Null(dto.Host);
        Assert.Empty(dto.Participants);
    }
}
