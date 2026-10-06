using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Webhooks;

/// <summary>
/// Dispatches event payloads to the generic webhook endpoints configured by the admin
/// (e.g. Discord or Telegram incoming webhook URLs). Implementations must never let a
/// delivery failure (network error, timeout, non-2xx response) propagate to the caller:
/// <see cref="NotifyAsync"/> is fire-and-forget from the perspective of the event pipeline.
/// </summary>
public interface IWebhookDispatcher
{
    /// <summary>
    /// Asynchronously delivers <paramref name="payload"/> to every configured, enabled
    /// endpoint subscribed to <paramref name="eventType"/>. Each delivery retries with a
    /// simple backoff and is logged on failure; failures never throw back to the caller.
    /// </summary>
    /// <param name="eventType">One of the well-known names in <c>WebhookEventTypes</c>.</param>
    /// <param name="payload">The JSON-serializable payload to post.</param>
    void Notify(string eventType, object payload);

    /// <summary>
    /// Sends a single test POST to <paramref name="url"/> with a synthetic payload and
    /// returns whether the endpoint accepted it (2xx response). Used by the configuration
    /// UI to validate a URL before saving it. Does not consult persisted configuration and
    /// does not retry.
    /// </summary>
    /// <param name="url">The absolute http(s) URL to test.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task resolving to a tuple of success and an optional detail message.</returns>
    Task<WebhookTestResult> TestAsync(string url, CancellationToken cancellationToken);
}

/// <summary>
/// The result of a single test delivery attempt against a webhook URL.
/// </summary>
/// <param name="Success">Whether the endpoint responded with a success (2xx) status code.</param>
/// <param name="Message">A human readable detail (status code or exception message).</param>
public sealed record WebhookTestResult(bool Success, string Message);
