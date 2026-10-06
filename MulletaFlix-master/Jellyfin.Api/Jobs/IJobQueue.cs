using System;
using System.Threading;
using System.Threading.Tasks;

namespace MulletaFlix.Api.Jobs;

/// <summary>
/// Internal background job queue for long running MulletaFlix work.
/// </summary>
public interface IJobQueue
{
    JobQueueItemDto Enqueue(
        string kind,
        string title,
        Func<CancellationToken, IProgress<JobQueueProgress>, Task> handler,
        string? correlationId = null);

    JobQueueStatusDto GetStatus();

    JobQueueItemDto? GetJob(string id);

    /// <summary>
    /// Waits for a queued or running job to reach a terminal state without polling.
    /// Completion is signalled by the worker via a <see cref="TaskCompletionSource{TResult}"/>,
    /// so the calling thread/slot is released while idle instead of spin-waiting on a timer.
    /// </summary>
    /// <param name="id">The job id returned by <see cref="Enqueue"/>.</param>
    /// <param name="onProgress">Optional callback invoked with the latest snapshot every time the job reports progress.</param>
    /// <param name="cancellationToken">Token used to stop waiting; does not cancel the job itself.</param>
    /// <returns>The final job snapshot, or <c>null</c> if no job with that id is known.</returns>
    Task<JobQueueItemDto?> WaitForCompletionAsync(string id, Action<JobQueueItemDto>? onProgress, CancellationToken cancellationToken);

    bool Cancel(string id);

    bool CancelByCorrelationId(string correlationId);

    int CancelAll();

    Task SetCacheAsync(string cacheKey, string value, TimeSpan ttl, CancellationToken cancellationToken);

    Task<string?> GetCacheAsync(string cacheKey, CancellationToken cancellationToken);
}
