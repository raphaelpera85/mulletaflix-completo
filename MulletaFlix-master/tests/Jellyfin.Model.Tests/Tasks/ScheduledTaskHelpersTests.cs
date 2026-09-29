using System;
using System.Collections.Generic;
using MediaBrowser.Model.Tasks;
using Xunit;

namespace Jellyfin.Model.Tests.Tasks;

public sealed class ScheduledTaskHelpersTests
{
    [Fact]
    public void GetNextExecutionTimeUtc_UsesServerLocalTimeForDailyTrigger()
    {
        var now = new DateTime(2026, 9, 28, 1, 0, 0, DateTimeKind.Local);
        var triggers = new List<TaskTriggerInfo>
        {
            new() { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks }
        };

        Assert.Equal(
            new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Local).ToUniversalTime(),
            ScheduledTaskHelpers.GetNextExecutionTimeUtc(triggers, now));
    }

    [Fact]
    public void GetNextExecutionTimeUtc_DailyTriggerAtExactTimeRemainsDueNow()
    {
        var now = new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Local);
        var triggers = new List<TaskTriggerInfo>
        {
            new() { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks }
        };

        Assert.Equal(now.ToUniversalTime(), ScheduledTaskHelpers.GetNextExecutionTimeUtc(triggers, now));
    }

    [Fact]
    public void GetNextExecutionTimeUtc_AdvancesPassedDailyAndWeeklyTriggers()
    {
        var now = new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Local);
        var triggers = new List<TaskTriggerInfo>
        {
            new() { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(2).Ticks },
            new() { Type = TaskTriggerInfoType.WeeklyTrigger, DayOfWeek = DayOfWeek.Monday, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks }
        };

        Assert.Equal(
            new DateTime(2026, 9, 29, 2, 0, 0, DateTimeKind.Local).ToUniversalTime(),
            ScheduledTaskHelpers.GetNextExecutionTimeUtc(triggers, now));
    }

    [Fact]
    public void GetNextExecutionTimeUtc_UsesConfiguredWeekdayAndServerLocalTime()
    {
        var now = new DateTime(2026, 9, 28, 1, 0, 0, DateTimeKind.Local);
        var triggers = new List<TaskTriggerInfo>
        {
            new() { Type = TaskTriggerInfoType.WeeklyTrigger, DayOfWeek = DayOfWeek.Wednesday, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks }
        };

        Assert.Equal(
            new DateTime(2026, 9, 30, 3, 0, 0, DateTimeKind.Local).ToUniversalTime(),
            ScheduledTaskHelpers.GetNextExecutionTimeUtc(triggers, now));
    }

    [Fact]
    public void GetNextExecutionTimeUtc_ReturnsNullForUnsupportedOrInvalidSchedules()
    {
        var now = new DateTime(2026, 9, 28, 1, 0, 0, DateTimeKind.Local);

        Assert.Null(ScheduledTaskHelpers.GetNextExecutionTimeUtc(
            [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(1).Ticks }], now));
        Assert.Null(ScheduledTaskHelpers.GetNextExecutionTimeUtc(
            [new TaskTriggerInfo { Type = TaskTriggerInfoType.WeeklyTrigger, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks }], now));
        Assert.Null(ScheduledTaskHelpers.GetNextExecutionTimeUtc(
            [new TaskTriggerInfo { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.TicksPerDay }], now));
        Assert.Null(ScheduledTaskHelpers.GetNextExecutionTimeUtc([], now));
    }
}
