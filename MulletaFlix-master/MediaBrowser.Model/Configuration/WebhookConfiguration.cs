using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.Configuration;

/// <summary>
/// Persisted configuration for generic event webhooks (e.g. Discord, Telegram, or any
/// service that accepts an HTTP POST with a JSON body). Configured endpoints receive a
/// POST whenever an enabled event type is published through <c>IEventManager</c>.
/// </summary>
public class WebhookConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookConfiguration"/> class.
    /// </summary>
    public WebhookConfiguration()
    {
        Endpoints = new List<WebhookEndpointConfiguration>();
    }

    /// <summary>
    /// Gets or sets the configured webhook endpoints.
    /// </summary>
    public List<WebhookEndpointConfiguration> Endpoints { get; set; }
}

/// <summary>
/// A single webhook endpoint: a destination URL plus which event types it should receive.
/// </summary>
public class WebhookEndpointConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEndpointConfiguration"/> class.
    /// </summary>
    public WebhookEndpointConfiguration()
    {
        Id = Guid.NewGuid().ToString("N");
        EnabledEvents = new List<string>();
    }

    /// <summary>
    /// Gets or sets the unique identifier of this endpoint.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets a friendly label for this endpoint (e.g. "Discord #avisos", "Telegram grupo família").
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the destination URL that receives the JSON POST. Must be an absolute http(s) URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this endpoint is enabled. Disabled endpoints
    /// never receive events, even if <see cref="EnabledEvents"/> matches.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the set of event type names (see <c>WebhookEventTypes</c>) this endpoint
    /// should receive. An empty list means "all event types".
    /// </summary>
    public List<string> EnabledEvents { get; set; }
}

/// <summary>
/// Well-known event type names published to configured webhooks.
/// </summary>
public static class WebhookEventTypes
{
    /// <summary>Playback of an item started.</summary>
    public const string PlaybackStart = "PlaybackStart";

    /// <summary>Playback of an item stopped.</summary>
    public const string PlaybackStop = "PlaybackStop";

    /// <summary>A new media item was added to the library.</summary>
    public const string ItemAdded = "ItemAdded";

    /// <summary>All known event type names, used to validate configuration input.</summary>
    public static readonly string[] All =
    {
        PlaybackStart,
        PlaybackStop,
        ItemAdded
    };
}
