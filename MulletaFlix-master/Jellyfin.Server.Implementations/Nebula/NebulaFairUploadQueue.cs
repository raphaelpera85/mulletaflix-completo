using System;
using System.Collections.Generic;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Fair FIFO scheduler that bounds consecutive priority selections while regular work waits.
/// </summary>
internal sealed class NebulaFairUploadQueue<T>
    where T : class
{
    private const int MaxConsecutivePriorityItems = 4;
    private readonly object _sync = new();
    private readonly Queue<T> _priority = new();
    private readonly Queue<T> _pending = new();
    private int _consecutivePriorityItems;
    private int _capacity = int.MaxValue;

    public int PendingCount
    {
        get
        {
            lock (_sync)
            {
                return _priority.Count + _pending.Count;
            }
        }
    }

    public int Capacity => _capacity;

    public void SetCapacity(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        lock (_sync)
        {
            _capacity = capacity;
        }
    }

    public bool TryEnqueue(T item, bool prioritized, int? maximumPending = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_sync)
        {
            var limit = Math.Min(_capacity, maximumPending ?? _capacity);
            if (_priority.Count + _pending.Count >= limit)
            {
                return false;
            }

            (prioritized ? _priority : _pending).Enqueue(item);
            return true;
        }
    }

    public IReadOnlyList<T> PromotePending(Func<T, bool> isPrioritized)
    {
        ArgumentNullException.ThrowIfNull(isPrioritized);

        lock (_sync)
        {
            var remaining = new Queue<T>();
            var promoted = new List<T>();
            while (_pending.TryDequeue(out var item))
            {
                if (isPrioritized(item))
                {
                    _priority.Enqueue(item);
                    promoted.Add(item);
                }
                else
                {
                    remaining.Enqueue(item);
                }
            }

            while (remaining.TryDequeue(out var item))
            {
                _pending.Enqueue(item);
            }

            return promoted;
        }
    }

    public IReadOnlyList<(T Item, bool IsPriority)> SnapshotInDequeueOrder()
    {
        lock (_sync)
        {
            var priority = new Queue<T>(_priority);
            var pending = new Queue<T>(_pending);
            var consecutivePriorityItems = _consecutivePriorityItems;
            var snapshot = new List<(T Item, bool IsPriority)>(priority.Count + pending.Count);

            while (priority.Count > 0 || pending.Count > 0)
            {
                if (consecutivePriorityItems >= MaxConsecutivePriorityItems && pending.TryDequeue(out var regularItem))
                {
                    consecutivePriorityItems = 0;
                    snapshot.Add((regularItem, false));
                }
                else if (priority.TryDequeue(out var priorityItem))
                {
                    consecutivePriorityItems = Math.Min(consecutivePriorityItems + 1, MaxConsecutivePriorityItems);
                    snapshot.Add((priorityItem, true));
                }
                else if (pending.TryDequeue(out regularItem))
                {
                    consecutivePriorityItems = 0;
                    snapshot.Add((regularItem, false));
                }
            }

            return snapshot;
        }
    }

    public bool TryDequeue(out T? item, out bool isPriorityItem)
    {
        lock (_sync)
        {
            if (_consecutivePriorityItems >= MaxConsecutivePriorityItems && _pending.TryDequeue(out item))
            {
                _consecutivePriorityItems = 0;
                isPriorityItem = false;
                return true;
            }

            if (_priority.TryDequeue(out item))
            {
                if (_consecutivePriorityItems < MaxConsecutivePriorityItems)
                {
                    _consecutivePriorityItems++;
                }

                isPriorityItem = true;
                return true;
            }

            if (_pending.TryDequeue(out item))
            {
                _consecutivePriorityItems = 0;
                isPriorityItem = false;
                return true;
            }

            item = null;
            isPriorityItem = false;
            return false;
        }
    }
}
