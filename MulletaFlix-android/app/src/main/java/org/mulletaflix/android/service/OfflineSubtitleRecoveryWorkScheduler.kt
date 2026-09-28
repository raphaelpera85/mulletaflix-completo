package org.mulletaflix.android.service

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import dagger.hilt.android.qualifiers.ApplicationContext
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

internal const val OFFLINE_SUBTITLE_RECOVERY_BACKOFF_SECONDS = 30L
internal const val OFFLINE_SUBTITLE_RECOVERY_FOLLOW_UP_DELAY_MILLIS = 2_000L
internal const val OFFLINE_SUBTITLE_RECOVERY_WORK_NAME = "mulletaflix-offline-subtitle-recovery"

internal fun offlineSubtitleRecoveryConstraints(): Constraints =
    Constraints.Builder()
        .setRequiredNetworkType(NetworkType.CONNECTED)
        .build()

internal fun offlineSubtitleRecoveryBackoffPolicy(): BackoffPolicy = BackoffPolicy.EXPONENTIAL

internal fun offlineSubtitleRecoveryWorkPolicy(isTransientFollowUp: Boolean): ExistingWorkPolicy =
    if (isTransientFollowUp) ExistingWorkPolicy.REPLACE else ExistingWorkPolicy.KEEP

internal fun offlineSubtitleRecoverySessionChangeWorkPolicy(): ExistingWorkPolicy = ExistingWorkPolicy.REPLACE

@Singleton
class OfflineSubtitleRecoveryWorkScheduler @Inject constructor(
    @ApplicationContext context: Context,
) {
    private val workManager = WorkManager.getInstance(context)

    fun enqueue() = enqueue(offlineSubtitleRecoveryWorkPolicy(isTransientFollowUp = false))

    fun enqueueAfterTransientFailure() = enqueue(
        policy = offlineSubtitleRecoveryWorkPolicy(isTransientFollowUp = true),
        initialDelayMillis = OFFLINE_SUBTITLE_RECOVERY_FOLLOW_UP_DELAY_MILLIS,
    )

    fun enqueueAfterSessionChange() = enqueue(policy = offlineSubtitleRecoverySessionChangeWorkPolicy())

    private fun enqueue(policy: ExistingWorkPolicy, initialDelayMillis: Long = 0L) {
        val request = OneTimeWorkRequestBuilder<OfflineSubtitleRecoveryWorker>()
            .setConstraints(offlineSubtitleRecoveryConstraints())
            .apply {
                if (initialDelayMillis > 0L) {
                    setInitialDelay(initialDelayMillis, TimeUnit.MILLISECONDS)
                }
            }
            .setBackoffCriteria(
                offlineSubtitleRecoveryBackoffPolicy(),
                OFFLINE_SUBTITLE_RECOVERY_BACKOFF_SECONDS,
                TimeUnit.SECONDS,
            )
            .build()
        workManager.enqueueUniqueWork(OFFLINE_SUBTITLE_RECOVERY_WORK_NAME, policy, request)
    }

    fun cancel() {
        workManager.cancelUniqueWork(OFFLINE_SUBTITLE_RECOVERY_WORK_NAME)
    }
}
