package org.mulletaflix.android

import android.content.Context
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import coil.annotation.ExperimentalCoilApi
import coil.Coil
import coil.disk.DiskCache
import coil.memory.MemoryCache
import java.util.concurrent.TimeUnit

/** Clears volatile artwork caches hourly. Offline downloads are intentionally untouched. */
internal object ImageCacheCleanup {
    const val INTERVAL_HOURS = 1L
    private const val WORK_NAME = "hourly-image-cache-cleanup"

    @OptIn(ExperimentalCoilApi::class)
    fun clear(context: Context) {
        val imageLoader = Coil.imageLoader(context.applicationContext)
        clearArtworkCaches(imageLoader.memoryCache, imageLoader.diskCache)
    }

    fun schedule(context: Context) {
        val request = PeriodicWorkRequestBuilder<ImageCacheCleanupWorker>(
            INTERVAL_HOURS,
            TimeUnit.HOURS,
        )
            .setConstraints(Constraints.NONE)
            .build()
        WorkManager.getInstance(context.applicationContext).enqueueUniquePeriodicWork(
            WORK_NAME,
            ExistingPeriodicWorkPolicy.UPDATE,
            request,
        )
    }
}

@OptIn(ExperimentalCoilApi::class)
internal fun clearArtworkCaches(memoryCache: MemoryCache?, diskCache: DiskCache?) {
    memoryCache?.clear()
    diskCache?.clear()
}

internal class ImageCacheCleanupWorker(
    context: Context,
    parameters: WorkerParameters,
) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result = runCatching {
        ImageCacheCleanup.clear(applicationContext)
    }.fold(
        onSuccess = { Result.success() },
        onFailure = { Result.retry() },
    )
}
