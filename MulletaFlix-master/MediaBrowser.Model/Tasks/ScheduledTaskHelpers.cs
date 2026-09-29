using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Model.Tasks
{
    /// <summary>
    /// Class ScheduledTaskHelpers.
    /// </summary>
    public static class ScheduledTaskHelpers
    {
        /// <summary>
        /// Gets the task info.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <returns>TaskInfo.</returns>
        public static TaskInfo GetTaskInfo(IScheduledTaskWorker task)
        {
            var isHidden = false;

            if (task.ScheduledTask is IConfigurableScheduledTask configurableTask)
            {
                isHidden = configurableTask.IsHidden;
            }

            string key = task.ScheduledTask.Key;
            var nextExecutionTimeUtc = GetNextExecutionTimeUtc(task.Triggers, DateTime.Now);

            return new TaskInfo
            {
                Name = task.Name,
                CurrentProgressPercentage = task.CurrentProgress,
                State = task.State,
                Id = task.Id,
                LastExecutionResult = task.LastExecutionResult,
                NextExecutionTimeUtc = nextExecutionTimeUtc,
                NextExecutionTimeOffsetMinutes = nextExecutionTimeUtc.HasValue
                    ? (int)TimeZoneInfo.Local.GetUtcOffset(nextExecutionTimeUtc.Value).TotalMinutes
                    : null,

                Triggers = task.Triggers,

                Description = task.Description,
                Category = task.Category,
                IsHidden = isHidden,
                Key = key
            };
        }

        /// <summary>
        /// Calculates the next calendar-triggered execution using the server's local time zone.
        /// </summary>
        /// <param name="triggers">The configured task triggers.</param>
        /// <param name="localNow">The current local time on the server.</param>
        /// <returns>The next execution in UTC, or <c>null</c> if any trigger is not a valid calendar trigger.</returns>
        public static DateTime? GetNextExecutionTimeUtc(IReadOnlyList<TaskTriggerInfo> triggers, DateTime localNow)
        {
            if (triggers is null || triggers.Count == 0 || triggers.Any(trigger =>
                trigger.Type != TaskTriggerInfoType.DailyTrigger && trigger.Type != TaskTriggerInfoType.WeeklyTrigger))
            {
                return null;
            }

            var nextRuns = triggers.Select(trigger =>
            {
                if (trigger.TimeOfDayTicks is not long timeOfDayTicks
                    || timeOfDayTicks < 0
                    || timeOfDayTicks >= TimeSpan.TicksPerDay)
                {
                    return (DateTime?)null;
                }

                var candidate = localNow.Date.Add(TimeSpan.FromTicks(timeOfDayTicks));
                if (trigger.Type == TaskTriggerInfoType.WeeklyTrigger)
                {
                    if (trigger.DayOfWeek is not DayOfWeek dayOfWeek)
                    {
                        return null;
                    }

                    var daysUntilTrigger = ((int)dayOfWeek - (int)localNow.DayOfWeek + 7) % 7;
                    candidate = candidate.AddDays(daysUntilTrigger);
                    if (candidate <= localNow)
                    {
                        candidate = candidate.AddDays(7);
                    }
                }
                else if (candidate < localNow)
                {
                    candidate = candidate.AddDays(1);
                }

                return candidate;
            }).ToArray();

            if (nextRuns.Any(nextRun => nextRun is null))
            {
                return null;
            }

            return nextRuns.Min()!.Value.ToUniversalTime();
        }
    }
}
