package org.mulletaflix.android.service

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent

@EntryPoint
@InstallIn(SingletonComponent::class)
internal interface PlaybackIssueSyncEntryPoint {
    fun playbackIssueQueueSyncer(): PlaybackIssueQueueSyncer
}

internal class PlaybackIssueSyncWorker(
    context: Context,
    params: WorkerParameters,
) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        return try {
            val entryPoint = EntryPointAccessors.fromApplication(
                applicationContext,
                PlaybackIssueSyncEntryPoint::class.java,
            )
            when (entryPoint.playbackIssueQueueSyncer().sync()) {
                PlaybackIssueSyncOutcome.COMPLETE,
                PlaybackIssueSyncOutcome.SESSION_REQUIRED,
                PlaybackIssueSyncOutcome.SESSION_CHANGED,
                -> Result.success()
                PlaybackIssueSyncOutcome.RETRY -> Result.retry()
            }
        } catch (cancelled: kotlinx.coroutines.CancellationException) {
            throw cancelled
        } catch (_: Exception) {
            Result.retry()
        }
    }
}
