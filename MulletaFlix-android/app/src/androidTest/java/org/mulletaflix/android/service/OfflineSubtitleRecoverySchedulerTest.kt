package org.mulletaflix.android.service

import android.content.Context
import android.os.SystemClock
import androidx.test.core.app.ApplicationProvider
import androidx.work.NetworkType
import androidx.work.BackoffPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.Configuration
import androidx.work.ListenableWorker
import androidx.work.WorkInfo
import androidx.work.WorkManager
import androidx.work.impl.WorkManagerImpl
import androidx.work.WorkerFactory
import androidx.work.WorkerParameters
import androidx.work.testing.WorkManagerTestInitHelper
import androidx.work.testing.WorkManagerTestInitHelper.ExecutorsMode
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class OfflineSubtitleRecoverySchedulerTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private val recoveryRuns = AtomicInteger()
    private lateinit var workManager: WorkManager
    private lateinit var scheduler: OfflineSubtitleRecoveryWorkScheduler
    private var originalWorkManager: WorkManagerImpl? = null

    @Before
    fun initializeWorkManager() {
        recoveryRuns.set(0)
        originalWorkManager = WorkManagerImpl.getInstance(context)
        val workerFactory = object : WorkerFactory() {
            override fun createWorker(
                appContext: Context,
                workerClassName: String,
                workerParameters: WorkerParameters,
            ): ListenableWorker? = if (workerClassName == OfflineSubtitleRecoveryWorker::class.java.name) {
                CountingRecoveryWorker(appContext, workerParameters, recoveryRuns)
            } else {
                null
            }
        }
        val configuration = Configuration.Builder()
            .setWorkerFactory(workerFactory)
            .build()
        WorkManagerTestInitHelper.initializeTestWorkManager(
            context,
            configuration,
            ExecutorsMode.LEGACY_OVERRIDE_WITH_SYNCHRONOUS_EXECUTORS,
        )
        workManager = WorkManager.getInstance(context)
        scheduler = OfflineSubtitleRecoveryWorkScheduler(context)
    }

    @After
    fun cleanWorkManager() {
        workManager.cancelUniqueWork(OFFLINE_SUBTITLE_RECOVERY_WORK_NAME).result.get()
        WorkManagerTestInitHelper.closeWorkDatabase()
        WorkManagerImpl.setDelegate(originalWorkManager)
    }

    @Test
    fun recoveryWorkRequiresNetworkAndUsesExponentialBackoff() {
        val constraints = offlineSubtitleRecoveryConstraints()

        assertEquals(NetworkType.CONNECTED, constraints.requiredNetworkType)
        assertEquals(BackoffPolicy.EXPONENTIAL, offlineSubtitleRecoveryBackoffPolicy())
        assertEquals(30L, OFFLINE_SUBTITLE_RECOVERY_BACKOFF_SECONDS)
        assertEquals(2_000L, OFFLINE_SUBTITLE_RECOVERY_FOLLOW_UP_DELAY_MILLIS)
        assertEquals(ExistingWorkPolicy.KEEP, offlineSubtitleRecoveryWorkPolicy(isTransientFollowUp = false))
        assertEquals(ExistingWorkPolicy.REPLACE, offlineSubtitleRecoverySessionChangeWorkPolicy())
        assertEquals(
            ExistingWorkPolicy.REPLACE,
            offlineSubtitleRecoveryWorkPolicy(isTransientFollowUp = true),
        )
    }

    @Test
    fun sessionChangeCancelsTheExistingUniqueWorkAndEnqueuesOneReplacement() {
        scheduler.enqueue()
        val stale = activeWork().single()

        scheduler.enqueueAfterSessionChange()
        val replacement = activeWork().single()

        assertNotEquals(stale.id, replacement.id)
        assertEquals(WorkInfo.State.ENQUEUED, replacement.state)
        assertEquals(0L, replacement.initialDelayMillis)
        assertCancelledOrPruned(stale.id)

        val testDriver = requireNotNull(WorkManagerTestInitHelper.getTestDriver(context))
        testDriver.setAllConstraintsMet(replacement.id)
        assertEquals(WorkInfo.State.SUCCEEDED, awaitState(replacement.id, WorkInfo.State.SUCCEEDED).state)
        assertEquals(1, recoveryRuns.get())
    }

    @Test
    fun logoutCancelsPendingUniqueRecoveryWork() {
        scheduler.enqueue()
        val pending = activeWork().single()

        scheduler.cancel()

        assertTrue(activeWork().isEmpty())
        assertCancelledOrPruned(pending.id)
    }

    @Test
    fun transientFailureBurstKeepsLatestDelayedFollowUpAndRunsItOnce() {
        val enqueuedIds = mutableListOf<java.util.UUID>()
        repeat(3) {
            scheduler.enqueueAfterTransientFailure()
            enqueuedIds += activeWork().single().id
        }

        val active = activeWork()
        assertEquals(1, active.size)
        val latest = active.single()
        assertEquals(3, enqueuedIds.distinct().size)
        enqueuedIds.dropLast(1).forEach(::assertCancelledOrPruned)
        assertEquals(WorkInfo.State.ENQUEUED, latest.state)
        assertEquals(OFFLINE_SUBTITLE_RECOVERY_FOLLOW_UP_DELAY_MILLIS, latest.initialDelayMillis)

        val testDriver = requireNotNull(WorkManagerTestInitHelper.getTestDriver(context))
        testDriver.setAllConstraintsMet(latest.id)
        assertEquals(0, recoveryRuns.get())

        testDriver.setInitialDelayMet(latest.id)
        val completed = awaitState(latest.id, WorkInfo.State.SUCCEEDED)

        assertEquals(WorkInfo.State.SUCCEEDED, completed.state)
        assertEquals(1, recoveryRuns.get())
        assertTrue(activeWork().isEmpty())
    }

    private fun activeWork(): List<WorkInfo> = workManager
        .getWorkInfosForUniqueWork(OFFLINE_SUBTITLE_RECOVERY_WORK_NAME)
        .get()
        .filter {
            it.state == WorkInfo.State.ENQUEUED ||
                it.state == WorkInfo.State.BLOCKED ||
                it.state == WorkInfo.State.RUNNING
        }

    private fun awaitState(id: java.util.UUID, expected: WorkInfo.State): WorkInfo {
        val deadline = SystemClock.elapsedRealtime() + TimeUnit.SECONDS.toMillis(5)
        var latest = requireNotNull(workManager.getWorkInfoById(id).get())
        while (latest.state != expected && SystemClock.elapsedRealtime() < deadline) {
            Thread.sleep(20)
            latest = requireNotNull(workManager.getWorkInfoById(id).get())
        }
        return latest
    }

    private fun assertCancelledOrPruned(id: java.util.UUID) {
        val info = workManager.getWorkInfoById(id).get()
        assertTrue("Expected $id to be cancelled or pruned, but was ${info?.state}",
            info == null || info.state == WorkInfo.State.CANCELLED)
    }

    private class CountingRecoveryWorker(
        context: Context,
        parameters: WorkerParameters,
        private val runCount: AtomicInteger,
    ) : OfflineSubtitleRecoveryWorker(context, parameters) {
        override suspend fun recoverPendingSubtitleCaching(): Boolean {
            runCount.incrementAndGet()
            return true
        }
    }
}
