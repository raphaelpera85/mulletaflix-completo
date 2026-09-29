#nullable disable
using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.Tasks
{
    /// <summary>
    /// Class TaskInfo.
    /// </summary>
    public class TaskInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskInfo"/> class.
        /// </summary>
        public TaskInfo()
        {
            Triggers = [];
        }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the state of the task.
        /// </summary>
        /// <value>The state of the task.</value>
        public TaskState State { get; set; }

        /// <summary>
        /// Gets or sets the progress.
        /// </summary>
        /// <value>The progress.</value>
        public double? CurrentProgressPercentage { get; set; }

        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the last execution result.
        /// </summary>
        /// <value>The last execution result.</value>
        public TaskResult LastExecutionResult { get; set; }

        /// <summary>
        /// Gets or sets the next scheduled execution time in UTC when it can be determined from calendar triggers.
        /// </summary>
        /// <value>The next scheduled execution time in UTC, or <c>null</c> when the schedule is not calendar-based.</value>
        public DateTime? NextExecutionTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the server's UTC offset in minutes at the next scheduled execution time.
        /// </summary>
        /// <value>The server's UTC offset in minutes, or <c>null</c> when no calendar execution is known.</value>
        public int? NextExecutionTimeOffsetMinutes { get; set; }

        /// <summary>
        /// Gets or sets the triggers.
        /// </summary>
        /// <value>The triggers.</value>
        public IReadOnlyList<TaskTriggerInfo> Triggers { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>The description.</value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        /// <value>The category.</value>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is hidden.
        /// </summary>
        /// <value><c>true</c> if this instance is hidden; otherwise, <c>false</c>.</value>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        /// <value>The key.</value>
        public string Key { get; set; }
    }
}
