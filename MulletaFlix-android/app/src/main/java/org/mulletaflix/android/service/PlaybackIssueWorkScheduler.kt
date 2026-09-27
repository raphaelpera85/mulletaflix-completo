package org.mulletaflix.android.service

import android.content.Context
import androidx.work.Constraints
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
internal class PlaybackIssueWorkScheduler @Inject constructor(
    @param:ApplicationContext context: Context,
) {
    private val workManager = WorkManager.getInstance(context)

    fun enqueue() {
        val request = OneTimeWorkRequestBuilder<PlaybackIssueSyncWorker>()
            .setConstraints(
                Constraints.Builder()
                    .setRequiredNetworkType(NetworkType.CONNECTED)
                    .build(),
            )
            .build()
        workManager.enqueueUniqueWork(WORK_NAME, ExistingWorkPolicy.APPEND_OR_REPLACE, request)
    }

    private companion object {
        const val WORK_NAME = "mulletaflix-playback-issue-sync"
    }
}
