using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaFairUploadQueueTests
{
    [Fact]
    public void TryDequeue_RegularUploadGetsTurnAfterFourPriorities()
    {
        var queue = new NebulaFairUploadQueue<string>();
        for (var index = 0; index < 8; index++)
        {
            Assert.True(queue.TryEnqueue($"priority-{index}", prioritized: true));
        }

        Assert.True(queue.TryEnqueue("regular", prioritized: false));

        var firstFive = new List<string>();
        var priorityFlags = new List<bool>();
        for (var index = 0; index < 5; index++)
        {
            Assert.True(queue.TryDequeue(out var item, out var isPriority));
            firstFive.Add(item!);
            priorityFlags.Add(isPriority);
        }

        Assert.Equal(new[] { "priority-0", "priority-1", "priority-2", "priority-3", "regular" }, firstFive);
        Assert.Equal(new[] { true, true, true, true, false }, priorityFlags);
    }

    [Fact]
    public void TryDequeue_DoesNotHoldRegularTurnWhenRegularQueueIsEmpty()
    {
        var queue = new NebulaFairUploadQueue<string>();
        for (var index = 0; index < 8; index++)
        {
            Assert.True(queue.TryEnqueue($"priority-{index}", prioritized: true));
        }

        for (var index = 0; index < 8; index++)
        {
            Assert.True(queue.TryDequeue(out var item, out var isPriority));
            Assert.Equal($"priority-{index}", item);
            Assert.True(isPriority);
        }

        Assert.False(queue.TryDequeue(out _, out _));
    }

    [Fact]
    public void SnapshotInDequeueOrder_ReflectsFairnessWithoutConsumingQueue()
    {
        var queue = new NebulaFairUploadQueue<string>();
        for (var index = 0; index < 6; index++)
        {
            Assert.True(queue.TryEnqueue($"priority-{index}", prioritized: true));
        }

        Assert.True(queue.TryEnqueue("regular-1", prioritized: false));
        Assert.True(queue.TryEnqueue("regular-2", prioritized: false));

        var snapshot = queue.SnapshotInDequeueOrder();

        Assert.Equal(
            new[] { "priority-0", "priority-1", "priority-2", "priority-3", "regular-1", "priority-4", "priority-5", "regular-2" },
            snapshot.Select(entry => entry.Item));
        Assert.Equal(8, queue.PendingCount);
        Assert.True(queue.TryDequeue(out var next, out var isPriority));
        Assert.Equal(snapshot[0].Item, next);
        Assert.Equal(snapshot[0].IsPriority, isPriority);
    }

    [Fact]
    public void SnapshotInDequeueOrder_UsesCurrentFairnessTurn()
    {
        var queue = new NebulaFairUploadQueue<string>();
        for (var index = 0; index < 6; index++)
        {
            Assert.True(queue.TryEnqueue($"priority-{index}", prioritized: true));
        }

        Assert.True(queue.TryEnqueue("regular", prioritized: false));
        for (var index = 0; index < 3; index++)
        {
            Assert.True(queue.TryDequeue(out _, out _));
        }

        var snapshot = queue.SnapshotInDequeueOrder();

        Assert.Equal("priority-3", snapshot[0].Item);
        Assert.Equal("regular", snapshot[1].Item);
        Assert.False(snapshot[1].IsPriority);
        Assert.True(queue.TryDequeue(out var next, out var isPriority));
        Assert.Equal(snapshot[0].Item, next);
        Assert.Equal(snapshot[0].IsPriority, isPriority);
    }

    [Fact]
    public void PromotePending_MovesOnlyMatchingItemsAndPreservesFifo()
    {
        var queue = new NebulaFairUploadQueue<string>();
        Assert.True(queue.TryEnqueue("request-1", prioritized: false));
        Assert.True(queue.TryEnqueue("regular", prioritized: false));
        Assert.True(queue.TryEnqueue("request-2", prioritized: false));

        var promoted = queue.PromotePending(item => item.StartsWith("request", System.StringComparison.Ordinal));

        Assert.Equal(new[] { "request-1", "request-2" }, promoted);
        Assert.True(queue.TryDequeue(out var first, out var firstIsPriority));
        Assert.Equal("request-1", first);
        Assert.True(firstIsPriority);
        Assert.True(queue.TryDequeue(out var second, out var secondIsPriority));
        Assert.Equal("request-2", second);
        Assert.True(secondIsPriority);
        Assert.True(queue.TryDequeue(out var third, out var thirdIsPriority));
        Assert.Equal("regular", third);
        Assert.False(thirdIsPriority);
    }

    [Fact]
    public void TryEnqueue_RespectsProducerLimitAndLeavesReservedCapacityForRequeues()
    {
        var queue = new NebulaFairUploadQueue<string>();
        queue.SetCapacity(4);

        Assert.True(queue.TryEnqueue("regular-1", prioritized: false, maximumPending: 2));
        Assert.True(queue.TryEnqueue("regular-2", prioritized: false, maximumPending: 2));
        Assert.False(queue.TryEnqueue("overflow", prioritized: false, maximumPending: 2));

        // Worker-owned retry can consume reserved room, but may never exceed total capacity.
        Assert.True(queue.TryEnqueue("retry-1", prioritized: true));
        Assert.True(queue.TryEnqueue("retry-2", prioritized: true));
        Assert.False(queue.TryEnqueue("overflow-retry", prioritized: true));
        Assert.Equal(4, queue.PendingCount);
    }

    [Fact]
    public void TryEnqueue_ConcurrentProducersNeverExceedCapacity()
    {
        var queue = new NebulaFairUploadQueue<string>();
        queue.SetCapacity(32);

        Parallel.For(0, 256, index => queue.TryEnqueue($"file-{index}", prioritized: index % 2 == 0));

        Assert.Equal(32, queue.PendingCount);
    }
}
