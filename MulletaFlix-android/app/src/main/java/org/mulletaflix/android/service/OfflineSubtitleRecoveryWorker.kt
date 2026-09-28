package org.mulletaflix.android.service

import android.content.Context
import androidx.media3.common.util.UnstableApi
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.currentCoroutineContext

@EntryPoint
@InstallIn(SingletonComponent::class)
@UnstableApi
internal interface OfflineSubtitleRecoveryEntryPoint {
    fun media3DownloadRepository(): Media3DownloadRepository
}

@UnstableApi
internal open class OfflineSubtitleRecoveryWorker(
    context: Context,
    params: WorkerParameters,
) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result = try {
        if (recoverPendingSubtitleCaching()) {
            Result.success()
        } else {
            Result.retry()
        }
    } catch (cancelled: kotlinx.coroutines.CancellationException) {
        throw cancelled
    } catch (_: Exception) {
        Result.failure()
    }

    protected open suspend fun recoverPendingSubtitleCaching(): Boolean {
        val entryPoint = EntryPointAccessors.fromApplication(
            applicationContext,
            OfflineSubtitleRecoveryEntryPoint::class.java,
        )
        val workerScope = CoroutineScope(currentCoroutineContext())
        return entryPoint.media3DownloadRepository().recoverPendingSubtitleCachingForWork(workerScope)
    }
}
