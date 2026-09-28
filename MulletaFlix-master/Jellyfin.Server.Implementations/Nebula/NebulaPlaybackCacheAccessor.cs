using System;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>Provides an atomically replaceable playback-cache reference to all Nebula stream surfaces.</summary>
public sealed class NebulaPlaybackCacheAccessor
{
    private readonly object _gate = new();
    private NebulaPlaybackCache? _current;

    public NebulaPlaybackCache? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Set(NebulaPlaybackCache? cache)
    {
        lock (_gate)
        {
            _current = cache;
        }
    }

    public (NebulaPlaybackCache? Cache, IDisposable? Lease) Acquire(string mediaKey)
    {
        lock (_gate)
        {
            var cache = _current;
            return (cache, cache?.Acquire(mediaKey));
        }
    }
}
