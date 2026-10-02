using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>Low-cardinality telemetry for scheduled STRM probing.</summary>
public static class StrmProbeMetrics
{
    public const string MeterName = "MulletaFlix.Server.StrmProbe";

    internal enum Result
    {
        Success,
        NoItems,
        PartialFailure,
        Failure,
        Cancelled
    }

    private static readonly Meter ProbeMeter = new(MeterName);
    private static readonly Counter<long> Runs = ProbeMeter.CreateCounter<long>("mulletaflix.strm_probe.runs");
    private static readonly Counter<long> ScannedItems = ProbeMeter.CreateCounter<long>("mulletaflix.strm_probe.items.scanned");
    private static readonly Counter<long> CandidateItems = ProbeMeter.CreateCounter<long>("mulletaflix.strm_probe.items.candidates");
    private static readonly Counter<long> FailedItems = ProbeMeter.CreateCounter<long>("mulletaflix.strm_probe.items.failed");
    private static readonly Histogram<double> Duration = ProbeMeter.CreateHistogram<double>("mulletaflix.strm_probe.duration", "ms");
    private static readonly UpDownCounter<long> ActiveRuns = ProbeMeter.CreateUpDownCounter<long>("mulletaflix.strm_probe.active_runs");

    internal static RunScope BeginRun()
    {
        ActiveRuns.Add(1);
        return new RunScope();
    }

    internal static void RecordScannedItems(int count)
    {
        if (count > 0)
        {
            ScannedItems.Add(count);
        }
    }

    internal static void RecordCandidateItems(int count)
    {
        if (count > 0)
        {
            CandidateItems.Add(count);
        }
    }

    internal static void RecordFailedItem()
    {
        FailedItems.Add(1);
    }

    internal sealed class RunScope : IDisposable
    {
        private readonly long _startedAt = Stopwatch.GetTimestamp();
        private string _result = "failure";
        private bool _completed;
        private bool _disposed;

        internal void Complete(Result result)
        {
            if (_completed)
            {
                return;
            }

            _result = result switch
            {
                Result.Success => "success",
                Result.NoItems => "no_items",
                Result.PartialFailure => "partial_failure",
                Result.Failure => "failure",
                Result.Cancelled => "cancelled",
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
            };
            _completed = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var tags = new TagList { { "result", _result } };
            Runs.Add(1, tags);
            Duration.Record(Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds, tags);
            ActiveRuns.Add(-1);
        }
    }
}
