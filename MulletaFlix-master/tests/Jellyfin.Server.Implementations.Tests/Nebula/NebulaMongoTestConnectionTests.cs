using System;
using MongoDB.Driver;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaMongoTestConnectionTests
{
    [Fact]
    public void IsAllowedTarget_AcceptsOnlyDedicatedLocalTestMongo()
    {
        Assert.True(
            NebulaMongoTestConnection.IsAllowedTarget("mongodb://127.0.0.1:27099/?serverSelectionTimeoutMS=1500", out var validReason),
            validReason);

        string?[] invalidTargets =
        [
            null,
            string.Empty,
            "mongodb://127.0.0.1:27017/",
            "mongodb://mongo.example.invalid:27099/",
            "mongodb://user:secret@127.0.0.1:27099/",
            "mongodb://127.0.0.1:27099,127.0.0.2:27099/",
            "mongodb+srv://mongo.example.invalid/"
        ];

        foreach (var target in invalidTargets)
        {
            Assert.False(NebulaMongoTestConnection.IsAllowedTarget(target, out var reason), target ?? "null");
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }

    [Fact]
    public void IsolatedConnectionString_DisablesReplicaSetDiscovery()
    {
        var settings = MongoClientSettings.FromConnectionString(NebulaMongoTestConnection.IsolatedConnectionString);

        Assert.True(settings.DirectConnection);
        Assert.Equal("127.0.0.1", settings.Server.Host);
        Assert.Equal(27099, settings.Server.Port);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), settings.ServerSelectionTimeout);
    }
}
