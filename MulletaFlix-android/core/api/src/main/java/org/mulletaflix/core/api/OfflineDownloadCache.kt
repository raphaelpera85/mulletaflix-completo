package org.mulletaflix.core.api

import android.content.Context
import androidx.media3.common.util.UnstableApi
import androidx.media3.database.StandaloneDatabaseProvider
import androidx.media3.datasource.cache.Cache
import androidx.media3.datasource.cache.NoOpCacheEvictor
import androidx.media3.datasource.cache.SimpleCache

/** Single cache instance shared by DownloadManager and the offline player. */
@UnstableApi
object OfflineDownloadCache {
    @Volatile
    private var cache: SimpleCache? = null

    @Synchronized
    fun get(context: Context): Cache = cache ?: SimpleCache(
        context.applicationContext.cacheDir.resolve("downloads"),
        NoOpCacheEvictor(),
        StandaloneDatabaseProvider(context.applicationContext),
    ).also { cache = it }
}
