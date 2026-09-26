using System;
using System.Collections.Concurrent;
using MediaBrowser.Controller.Entities;

namespace MulletaFlix.Api.Helpers;

/// <summary>
/// Keeps path-resolved playback items available to streaming endpoints without persisting them.
/// </summary>
public sealed class TransientMediaItemRegistry
{
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromHours(3);
    private readonly ConcurrentDictionary<Guid, Entry> _items = new();

    /// <summary>
    /// Registers an item resolved from a path for temporary playback.
    /// </summary>
    /// <param name="item">The resolved item.</param>
    public void Register(BaseItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _items[item.Id] = new Entry(item, now.Add(EntryLifetime));

        foreach (var pair in _items)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _items.TryRemove(pair.Key, out _);
            }
        }
    }

    /// <summary>
    /// Gets a temporarily registered item if it has not expired.
    /// </summary>
    /// <param name="id">The item identifier.</param>
    /// <param name="item">The resolved item.</param>
    /// <returns><see langword="true"/> when an unexpired item was found.</returns>
    public bool TryGet(Guid id, out BaseItem? item)
    {
        if (_items.TryGetValue(id, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                item = entry.Item;
                return true;
            }

            _items.TryRemove(id, out _);
        }

        item = null;
        return false;
    }

    private sealed record Entry(BaseItem Item, DateTimeOffset ExpiresAt);
}
