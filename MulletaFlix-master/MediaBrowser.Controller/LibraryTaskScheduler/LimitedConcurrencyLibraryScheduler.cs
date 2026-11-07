using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Controller.LibraryTaskScheduler;

/// <summary>
/// Provides Parallel action interface to process tasks with a set concurrency level.
/// </summary>
public sealed class LimitedConcurrencyLibraryScheduler : ILimitedConcurrencyLibraryScheduler, IAsyncDisposable
{
    private const int CleanupGracePeriod = 60;
    private readonly TimeSpan _cleanupGracePeriod;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<LimitedConcurrencyLibraryScheduler> _logger;
    private readonly IServerConfigurationManager _serverConfigurationManager;
    private readonly Dictionary<CancellationTokenSource, Task> _taskRunners = new();
    private readonly CancellationTokenSource _cleanupCancellationTokenSource = new();
    private readonly CancellationTokenSource _cleanupWaitCancellationTokenSource;

    private static readonly AsyncLocal<CancellationTokenSource> _deadlockDetector = new();
    private static readonly AsyncLocal<LimitedConcurrencyLibraryScheduler?> _currentWorkerScheduler = new();

    /// <summary>
    /// Gets used to lock all operations on the Tasks queue and creating workers.
    /// </summary>
    private readonly Lock _taskLock = new();

    private readonly Channel<TaskQueueItem> _tasks;

    private volatile int _workCounter;
    private int _enqueueCounter;
    private TaskCompletionSource? _enqueueDrained;
    private TaskCompletionSource? _disposeCompletion;
    private Task? _cleanupTask;
    private TaskCompletionSource? _cleanupInProgress;
    private bool _cleanupRequested;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitedConcurrencyLibraryScheduler"/> class.
    /// </summary>
    /// <param name="hostApplicationLifetime">The hosting lifetime.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="serverConfigurationManager">The server configuration manager.</param>
    public LimitedConcurrencyLibraryScheduler(
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<LimitedConcurrencyLibraryScheduler> logger,
        IServerConfigurationManager serverConfigurationManager)
        : this(hostApplicationLifetime, logger, serverConfigurationManager, TimeSpan.FromSeconds(CleanupGracePeriod))
    {
    }

    internal LimitedConcurrencyLibraryScheduler(
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<LimitedConcurrencyLibraryScheduler> logger,
        IServerConfigurationManager serverConfigurationManager,
        TimeSpan cleanupGracePeriod)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _serverConfigurationManager = serverConfigurationManager;
        _cleanupGracePeriod = cleanupGracePeriod;
        _cleanupWaitCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_hostApplicationLifetime.ApplicationStopping);
        _tasks = Channel.CreateBounded<TaskQueueItem>(new BoundedChannelOptions(Math.Max(1, CalculateScanConcurrencyLimit()))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    private void ScheduleTaskCleanup()
    {
        lock (_taskLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_cleanupTask is { IsCompleted: false })
            {
                _cleanupRequested = true;
                _logger.LogDebug("Cleanup task already scheduled.");
                // cleanup task is already running.
                return;
            }

            _cleanupTask = RunCleanupTask();
        }

        async Task RunCleanupTask()
        {
            _logger.LogDebug("Schedule cleanup task in {CleanupGracePeriod} sec.", _cleanupGracePeriod.TotalSeconds);
            try
            {
                await Task.Delay(_cleanupGracePeriod, _cleanupCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cleanupCancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (_disposed)
            {
                _logger.LogDebug("Abort cleaning up, already disposed.");
                return;
            }

            TaskCompletionSource? cleanupCompletion = null;
            try
            {
                KeyValuePair<CancellationTokenSource, Task>[] taskRunners;
                lock (_taskLock)
                {
                    if (_tasks.Reader.Count > 0 || _workCounter > 0 || _enqueueCounter > 0)
                    {
                        _logger.LogDebug("Delay cleanup task, operations still running.");
                        _cleanupRequested = true;
                        return;
                    }

                    cleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _cleanupInProgress = cleanupCompletion;
                    taskRunners = _taskRunners.ToArray();
                }

                _logger.LogDebug("Cleanup runners.");
                await CancelAndWaitForRunnersAsync(taskRunners).ConfigureAwait(false);
                lock (_taskLock)
                {
                    if (ReferenceEquals(_cleanupInProgress, cleanupCompletion))
                    {
                        _cleanupInProgress = null;
                        cleanupCompletion.TrySetResult();
                    }
                }
            }
            finally
            {
                lock (_taskLock)
                {
                    if (cleanupCompletion is not null && ReferenceEquals(_cleanupInProgress, cleanupCompletion))
                    {
                        _cleanupInProgress = null;
                        cleanupCompletion.TrySetResult();
                    }

                    _cleanupTask = null;
                    if (_enqueueCounter > 0)
                    {
                        _cleanupRequested = true;
                    }

                    if (_cleanupRequested && !_disposed)
                    {
                        _cleanupRequested = false;
                        _cleanupTask = RunCleanupTask();
                    }
                }
            }
        }
    }

    private static Task CancelIfRunningAsync(CancellationTokenSource cancellationTokenSource)
    {
        return Task.Run(async () =>
        {
            try
            {
                await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The worker completed and disposed its token source before cleanup reached it.
            }
        });
    }

    internal static Task CancelAndWaitForRunnersAsync(KeyValuePair<CancellationTokenSource, Task>[] taskRunners)
    {
        var runnerTasks = taskRunners
            .Select(item => item.Value is Task<Task> worker ? worker.Unwrap() : item.Value);
        return Task.WhenAll(taskRunners.Select(item => CancelIfRunningAsync(item.Key)).Concat(runnerTasks));
    }

    private bool ShouldForceSequentialOperation()
    {
        // if the user either set the setting to 1 or it's unset and we have fewer than 4 cores it's better to run sequentially.
        var fanoutSetting = _serverConfigurationManager.Configuration.LibraryScanFanoutConcurrency;
        return fanoutSetting == 1 || (fanoutSetting <= 0 && Environment.ProcessorCount <= 3);
    }

    private int CalculateScanConcurrencyLimit()
    {
        // when this is invoked, we already checked ShouldForceSequentialOperation for the sequential check.
        var fanoutConcurrency = _serverConfigurationManager.Configuration.LibraryScanFanoutConcurrency;
        if (fanoutConcurrency <= 0)
        {
            // in case the user did not set a limit manually, we can assume he has 3 or more cores as already checked by ShouldForceSequentialOperation.
            return Environment.ProcessorCount - 3;
        }

        return fanoutConcurrency;
    }

    private void Worker()
    {
        lock (_taskLock)
        {
            var operationFanout = Math.Max(0, CalculateScanConcurrencyLimit() - _taskRunners.Count);
            _logger.LogDebug("Spawn {NumberRunners} new runners.", operationFanout);
            for (int i = 0; i < operationFanout; i++)
            {
                var workerLifetime = new CancellationTokenSource();
                var workerToken = CancellationTokenSource.CreateLinkedTokenSource(workerLifetime.Token, _hostApplicationLifetime.ApplicationStopping);
                _taskRunners.Add(
                    workerToken,
                    Task.Factory.StartNew(
                        ItemWorker,
                        (workerToken, workerLifetime),
                        CancellationToken.None,
                        TaskCreationOptions.PreferFairness,
                        TaskScheduler.Default));
            }
        }
    }

    private async Task ItemWorker(object? obj)
    {
        var worker = ((CancellationTokenSource Token, CancellationTokenSource Lifetime))obj!;
        var previousWorkerScheduler = _currentWorkerScheduler.Value;
        _currentWorkerScheduler.Value = this;
        _deadlockDetector.Value = worker.Token;
        try
        {
            while (!worker.Token.IsCancellationRequested)
            {
                var item = await _tasks.Reader.ReadAsync(worker.Token.Token).ConfigureAwait(false);
                try
                {
                    var newWorkerLimit = Interlocked.Increment(ref _workCounter) > 0;
                    Debug.Assert(newWorkerLimit, "_workCounter > 0");
                    _logger.LogDebug("Process new item '{Data}'.", item.Data);
                    await ProcessItem(item).ConfigureAwait(false);
                }
                finally
                {
                    var newWorkerLimit = Interlocked.Decrement(ref _workCounter) >= 0;
                    Debug.Assert(newWorkerLimit, "_workCounter > 0");
                }
            }
        }
        catch (OperationCanceledException) when (worker.Token.IsCancellationRequested)
        {
            // thats how you do it, interupt the waiter thread. There is nothing to do here when it was on purpose.
        }
        catch (ChannelClosedException) when (_disposed)
        {
            // Dispose completes the channel before canceling idle workers.
        }
        finally
        {
            _logger.LogDebug("Cleanup Runner'.");
            _currentWorkerScheduler.Value = previousWorkerScheduler;
            _deadlockDetector.Value = default!;
            lock (_taskLock)
            {
                _taskRunners.Remove(worker.Token);
            }

            worker.Lifetime.Dispose();
            worker.Token.Dispose();
        }
    }

    private async Task ProcessItem(TaskQueueItem item)
    {
        try
        {
            if (item.CancellationToken.IsCancellationRequested)
            {
                // if item is cancelled, just skip it
                return;
            }

            await item.Worker(item.Data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while performing a library operation");
        }
        finally
        {
            item.Progress.Report(100);
            item.Workset.CompleteItem();
        }
    }

    /// <inheritdoc/>
    public async Task Enqueue<T>(T[] data, Func<T, IProgress<double>, Task> worker, IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        if (data.Length == 0 || cancellationToken.IsCancellationRequested)
        {
            progress.Report(100);
            return;
        }

        _logger.LogDebug("Enqueue new Workset of {NoItems} items.", data.Length);

        var workset = new WorksetProgress(data.Length, progress);
        TaskQueueItem CreateTaskQueueItem(T item, int index)
        {
            TaskQueueItem queueItem = null!;
            return queueItem = new TaskQueueItem()
            {
                Data = item!,
                Progress = new ActionProgress(innerPercent => workset.Report(index, innerPercent)),
                Worker = val => worker((T)val, queueItem.Progress),
                CancellationToken = cancellationToken,
                Workset = workset
            };
        }

        var sequential = ShouldForceSequentialOperation() || _deadlockDetector.Value is not null;
        Task? cleanupInProgress;
        lock (_taskLock)
        {
            if (_disposed)
            {
                return;
            }

            _enqueueCounter++;
            _enqueueDrained ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cleanupInProgress = _cleanupInProgress?.Task;
        }

        try
        {
            if (sequential)
            {
                _logger.LogDebug("Process sequentially.");
                try
                {
                    for (var i = 0; i < data.Length; i++)
                    {
                        _hostApplicationLifetime.ApplicationStopping.ThrowIfCancellationRequested();
                        await ProcessItem(CreateTaskQueueItem(data[i], i)).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // operation is cancelled. Do nothing.
                }

                _logger.LogDebug("Process sequentially done.");
                return;
            }

            if (cleanupInProgress is not null)
            {
                await cleanupInProgress.WaitAsync(_cleanupWaitCancellationTokenSource.Token).ConfigureAwait(false);
            }

            Worker();
            for (var i = 0; i < data.Length; i++)
            {
                await _tasks.Writer.WriteAsync(CreateTaskQueueItem(data[i], i), _hostApplicationLifetime.ApplicationStopping).ConfigureAwait(false);
            }

            _logger.LogDebug("Wait for {NoWorkers} to complete.", data.Length);
            await workset.Completion.WaitAsync(_hostApplicationLifetime.ApplicationStopping).ConfigureAwait(false);
        }
        finally
        {
            lock (_taskLock)
            {
                _enqueueCounter--;
                if (_enqueueCounter == 0)
                {
                    _enqueueDrained?.TrySetResult();
                    _enqueueDrained = null;
                }
            }
        }

        _logger.LogDebug("{NoWorkers} completed.", data.Length);
        ScheduleTaskCleanup();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (ReferenceEquals(_currentWorkerScheduler.Value, this))
        {
            throw new InvalidOperationException("A library worker cannot dispose its scheduler while its enqueue is active.");
        }

        KeyValuePair<CancellationTokenSource, Task>[] taskRunners;
        Task? enqueuesDrained;
        Task? existingDispose;
        lock (_taskLock)
        {
            if (_disposed)
            {
                existingDispose = _disposeCompletion?.Task;
                enqueuesDrained = null;
                taskRunners = [];
            }
            else
            {
                _disposed = true;
                _disposeCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                existingDispose = null;
                _cleanupCancellationTokenSource.Cancel();
                _cleanupWaitCancellationTokenSource.Cancel();
                enqueuesDrained = _enqueueDrained?.Task;
            }
        }

        if (existingDispose is not null)
        {
            await existingDispose.ConfigureAwait(false);
            return;
        }

        try
        {
            if (enqueuesDrained is not null)
            {
                await enqueuesDrained.ConfigureAwait(false);
            }

            _tasks.Writer.TryComplete();
            lock (_taskLock)
            {
                _tasks.Writer.TryComplete();
                taskRunners = _taskRunners.ToArray();
            }

            await CancelAndWaitForRunnersAsync(taskRunners).ConfigureAwait(false);

            if (_cleanupTask is not null)
            {
                await _cleanupTask.ConfigureAwait(false);
                _cleanupTask?.Dispose();
            }
            _cleanupCancellationTokenSource.Dispose();
            _cleanupWaitCancellationTokenSource.Dispose();
            _disposeCompletion!.TrySetResult();
        }
        catch (Exception exception)
        {
            _disposeCompletion!.TrySetException(exception);
            throw;
        }
    }

    private class TaskQueueItem
    {
        public required object Data { get; init; }

        public required Func<object, Task> Worker { get; init; }

        public required IProgress<double> Progress { get; init; }

        public CancellationToken CancellationToken { get; init; }

        public required WorksetProgress Workset { get; init; }
    }

    private sealed class WorksetProgress
    {
        private readonly IProgress<double> _progress;
        private readonly double[] _itemProgress;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private long _totalProgress;
        private int _remaining;

        public WorksetProgress(int itemCount, IProgress<double> progress)
        {
            _itemProgress = new double[itemCount];
            _progress = progress;
            _remaining = itemCount;
        }

        public Task Completion => _completion.Task;

        public void Report(int index, double percent)
        {
            var roundedPercent = Math.Round(percent);
            var previousPercent = Interlocked.Exchange(ref _itemProgress[index], roundedPercent);
            if (previousPercent != roundedPercent)
            {
                var total = Interlocked.Add(ref _totalProgress, (long)(roundedPercent - previousPercent));
                _progress.Report(total / _itemProgress.Length);
            }
        }

        public void CompleteItem()
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
            {
                _completion.TrySetResult();
            }
        }
    }

    private sealed class ActionProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
