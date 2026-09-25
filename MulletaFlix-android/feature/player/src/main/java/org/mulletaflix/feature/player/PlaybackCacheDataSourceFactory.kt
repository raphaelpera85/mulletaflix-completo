package org.mulletaflix.feature.player

import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DataSource
import androidx.media3.datasource.cache.Cache
import androidx.media3.datasource.cache.CacheDataSource

/** Shares offline segments with playback without persisting bytes fetched only for streaming. */
@UnstableApi
internal fun playbackCacheDataSourceFactory(
    cache: Cache,
    upstreamFactory: DataSource.Factory,
): CacheDataSource.Factory = CacheDataSource.Factory()
    .setCache(cache)
    .setCacheWriteDataSinkFactory(null)
    .setUpstreamDataSourceFactory(upstreamFactory)
