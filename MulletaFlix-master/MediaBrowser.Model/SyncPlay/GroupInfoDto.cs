using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.SyncPlay
{
    /// <summary>
    /// Class GroupInfoDto.
    /// </summary>
    public class GroupInfoDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupInfoDto"/> class.
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="groupName">The group name.</param>
        /// <param name="state">The group state.</param>
        /// <param name="participants">The participants.</param>
        /// <param name="lastUpdatedAt">The date when this DTO has been created.</param>
        public GroupInfoDto(Guid groupId, string groupName, GroupStateType state, IReadOnlyList<string> participants, DateTime lastUpdatedAt)
        {
            GroupId = groupId;
            GroupName = groupName;
            State = state;
            Participants = participants;
            LastUpdatedAt = lastUpdatedAt;
        }

        /// <summary>
        /// Gets the group identifier.
        /// </summary>
        /// <value>The group identifier.</value>
        public Guid GroupId { get; }

        /// <summary>
        /// Gets the group name.
        /// </summary>
        /// <value>The group name.</value>
        public string GroupName { get; }

        /// <summary>
        /// Gets the group state.
        /// </summary>
        /// <value>The group state.</value>
        public GroupStateType State { get; }

        /// <summary>
        /// Gets the participants.
        /// </summary>
        /// <value>The participants.</value>
        public IReadOnlyList<string> Participants { get; }

        /// <summary>
        /// Gets the date when this DTO has been created.
        /// </summary>
        /// <value>The date when this DTO has been created.</value>
        public DateTime LastUpdatedAt { get; }

        /// <summary>
        /// Gets or sets the highest ping (latency in milliseconds) among the group members.
        /// </summary>
        /// <value>The highest ping in milliseconds.</value>
        public long Ping { get; set; }

        /// <summary>
        /// Gets or sets the host (first participant) of the group.
        /// </summary>
        /// <value>The host username, or null if the group is empty.</value>
        public string? Host { get; set; }
    }
}
