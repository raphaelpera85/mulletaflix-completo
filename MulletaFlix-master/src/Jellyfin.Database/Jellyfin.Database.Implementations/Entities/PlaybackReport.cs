using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MulletaFlix.Database.Implementations.Interfaces;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Database.Implementations.Entities
{
    /// <summary>
    /// Entity representing a playback session record for reporting.
    /// </summary>
    public class PlaybackReport : IHasConcurrencyToken
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackReport"/> class.
        /// </summary>
        public PlaybackReport()
        {
            DateCreated = DateTime.UtcNow;
            LogSeverity = LogLevel.Information;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackReport"/> class with required data.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="itemId">The item id.</param>
        /// <param name="itemName">The item name.</param>
        /// <param name="itemType">The item media type.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="deviceName">The device name.</param>
        /// <param name="clientName">The client name.</param>
        /// <param name="playSessionId">The play session id.</param>
        /// <param name="sessionId">The session id.</param>
        public PlaybackReport(
            Guid userId,
            Guid itemId,
            string itemName,
            string itemType,
            string deviceId,
            string deviceName,
            string clientName,
            string playSessionId,
            string sessionId)
        {
            ArgumentException.ThrowIfNullOrEmpty(itemName);
            ArgumentException.ThrowIfNullOrEmpty(itemType);
            ArgumentException.ThrowIfNullOrEmpty(deviceId);
            ArgumentException.ThrowIfNullOrEmpty(deviceName);
            ArgumentException.ThrowIfNullOrEmpty(clientName);
            ArgumentException.ThrowIfNullOrEmpty(playSessionId);
            ArgumentException.ThrowIfNullOrEmpty(sessionId);

            UserId = userId;
            ItemId = itemId;
            ItemName = itemName;
            ItemType = itemType;
            DeviceId = deviceId;
            DeviceName = deviceName;
            ClientName = clientName;
            PlaySessionId = playSessionId;
            SessionId = sessionId;
            DateCreated = DateTime.UtcNow;
            LogSeverity = LogLevel.Information;
        }

        /// <summary>
        /// Gets the identity of this instance.
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the item id.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        [MaxLength(512)]
        [StringLength(512)]
        public string ItemName { get; set; }

        /// <summary>
        /// Gets or sets the item media type (Movie, Series, Episode, Audio, etc.).
        /// </summary>
        [MaxLength(64)]
        [StringLength(64)]
        public string ItemType { get; set; }

        /// <summary>
        /// Gets or sets the series name (for episodes).
        /// </summary>
        [MaxLength(512)]
        [StringLength(512)]
        public string? SeriesName { get; set; }

        /// <summary>
        /// Gets or sets the season number (for episodes).
        /// </summary>
        public int? SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode number (for episodes).
        /// </summary>
        public int? EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets the artist (for audio).
        /// </summary>
        [MaxLength(256)]
        [StringLength(256)]
        public string? Artist { get; set; }

        /// <summary>
        /// Gets or sets the album (for audio).
        /// </summary>
        [MaxLength(256)]
        [StringLength(256)]
        public string? Album { get; set; }

        /// <summary>
        /// Gets or sets the device id.
        /// </summary>
        [MaxLength(256)]
        [StringLength(256)]
        public string DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        [MaxLength(256)]
        [StringLength(256)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the client name.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string ClientName { get; set; }

        /// <summary>
        /// Gets or sets the play session id.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string PlaySessionId { get; set; }

        /// <summary>
        /// Gets or sets the session id.
        /// </summary>
        [MaxLength(128)]
        [StringLength(128)]
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the start time in UTC.
        /// </summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the end time in UTC.
        /// </summary>
        public DateTime? EndTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the playback duration in seconds.
        /// </summary>
        public long? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the playback position at start in ticks.
        /// </summary>
        public long? StartPositionTicks { get; set; }

        /// <summary>
        /// Gets or sets the playback position at end in ticks.
        /// </summary>
        public long? EndPositionTicks { get; set; }

        /// <summary>
        /// Gets or sets the total runtime of the item in ticks.
        /// </summary>
        public long? ItemRuntimeTicks { get; set; }

        /// <summary>
        /// Gets or sets the completion percentage (0-100).
        /// </summary>
        public double? CompletionPercentage { get; set; }

        /// <summary>
        /// Gets or sets whether playback completed.
        /// </summary>
        public bool PlayedToCompletion { get; set; }

        /// <summary>
        /// Gets or sets whether playback was transcoded.
        /// </summary>
        public bool WasTranscoded { get; set; }

        /// <summary>
        /// Gets or sets the video codec used.
        /// </summary>
        [MaxLength(64)]
        [StringLength(64)]
        public string? VideoCodec { get; set; }

        /// <summary>
        /// Gets or sets the audio codec used.
        /// </summary>
        [MaxLength(64)]
        [StringLength(64)]
        public string? AudioCodec { get; set; }

        /// <summary>
        /// Gets or sets the container format.
        /// </summary>
        [MaxLength(32)]
        [StringLength(32)]
        public string? Container { get; set; }

        /// <summary>
        /// Gets or sets the streaming bitrate.
        /// </summary>
        public long? Bitrate { get; set; }

        /// <summary>
        /// Gets or sets the width of the video.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the video.
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Gets or sets the streaming protocol (HLS, DASH, HTTP, etc.).
        /// </summary>
        [MaxLength(32)]
        [StringLength(32)]
        public string? Protocol { get; set; }

        /// <summary>
        /// Gets or sets the streaming method (DirectPlay, DirectStream, Transcode).
        /// </summary>
        [MaxLength(32)]
        [StringLength(32)]
        public string? PlayMethod { get; set; }

        /// <summary>
        /// Gets or sets the remote end point (IP).
        /// </summary>
        [MaxLength(64)]
        [StringLength(64)]
        public string? RemoteEndPoint { get; set; }

        /// <summary>
        /// Gets or sets whether this was a local (LAN) playback.
        /// </summary>
        public bool IsLocal { get; set; }

        /// <summary>
        /// Gets or sets the library id the item belongs to.
        /// </summary>
        public Guid? LibraryId { get; set; }

        /// <summary>
        /// Gets or sets the library name.
        /// </summary>
        [MaxLength(256)]
        [StringLength(256)]
        public string? LibraryName { get; set; }

        /// <summary>
        /// Gets or sets the error message if playback failed.
        /// </summary>
        [MaxLength(1024)]
        [StringLength(1024)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the date created. This should be in UTC.
        /// </summary>
        public DateTime DateCreated { get; set; }

        /// <summary>
        /// Gets or sets the log severity. Default is <see cref="LogLevel.Information"/>.
        /// </summary>
        public LogLevel LogSeverity { get; set; }

        /// <inheritdoc />
        [ConcurrencyCheck]
        public uint RowVersion { get; private set; }

        /// <inheritdoc />
        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}