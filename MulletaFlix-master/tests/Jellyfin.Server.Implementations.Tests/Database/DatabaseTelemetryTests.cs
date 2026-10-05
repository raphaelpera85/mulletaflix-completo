using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Jellyfin.Server.Implementations.Database;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Database;

public sealed class DatabaseTelemetryTests
{
    [Fact]
    public void CommandAndConnectionMetrics_ExportOnlyBoundedOperationalTags()
    {
        var measurements = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == DatabaseTelemetryMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, CopyTags(tags))));
        listener.Start();

        DatabaseTelemetryMetrics.RecordCommand(0.125, "read", "movies", "success");
        DatabaseTelemetryMetrics.RecordConnectionOpen(0.25, "catalog", "failure");

        Assert.Contains(measurements, item => item.Name == "mulletaflix.database.command.duration"
            && item.Value == 0.125
            && item.Tags.Any(tag => tag.Key == "db.operation" && Equals(tag.Value, "read"))
            && item.Tags.Any(tag => tag.Key == "db.context" && Equals(tag.Value, "movies")));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.database.connection.open.duration"
            && item.Value == 0.25
            && item.Tags.Any(tag => tag.Key == "result" && Equals(tag.Value, "failure")));
        Assert.All(measurements, item => Assert.All(item.Tags, tag =>
            Assert.Contains(tag.Key, new[] { "db.operation", "db.context", "result" })));
    }

    [Theory]
    [InlineData("SELECT * FROM Items", "read")]
    [InlineData("/* EF tag */ UPDATE Items SET Name = @p0", "write")]
    [InlineData("-- migration\nCREATE TABLE Items (Id int)", "schema")]
    [InlineData("WITH Recent AS (SELECT 1) SELECT * FROM Recent", "with")]
    [InlineData("", "other")]
    public void ClassifyOperation_MapsSqlToFixedCategories(string sql, string expected)
    {
        Assert.Equal(expected, DatabaseCommandTelemetryInterceptor.ClassifyOperation(sql));
    }

    [Fact]
    public void ClassifyContext_UsesStableNamesInsteadOfArbitraryTypeNames()
    {
        Assert.Equal("unknown", DatabaseCommandTelemetryInterceptor.ClassifyContext(null));
        Assert.Equal("other", DatabaseCommandTelemetryInterceptor.ClassifyContext(new ArbitraryContext()));
    }

    private static KeyValuePair<string, object?>[] CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new KeyValuePair<string, object?>[tags.Length];
        for (var index = 0; index < tags.Length; index++)
        {
            copy[index] = tags[index];
        }

        return copy;
    }

    private sealed class ArbitraryContext : Microsoft.EntityFrameworkCore.DbContext
    {
    }
}
