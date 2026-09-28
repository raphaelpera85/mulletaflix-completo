package org.mulletaflix.android.service

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.work.ListenableWorker
import androidx.work.WorkerFactory
import androidx.work.WorkerParameters
import androidx.work.testing.TestListenableWorkerBuilder
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class OfflineSubtitleRecoveryWorkerTest {
    private val context: Context = ApplicationProvider.getApplicationContext()

    @Test
    fun completedRecoveryReturnsSuccess() = runBlocking {
        assertEquals(ListenableWorker.Result.success(), runWorker { true })
    }

    @Test
    fun incompleteRecoveryRequestsRetry() = runBlocking {
        assertEquals(ListenableWorker.Result.retry(), runWorker { false })
    }

    @Test
    fun unexpectedFailureReturnsFailure() = runBlocking {
        assertEquals(ListenableWorker.Result.failure(), runWorker { error("repository unavailable") })
    }

    @Test
    fun cancellationPropagatesToWorkManager() = runBlocking {
        try {
            runWorker { throw CancellationException("worker stopped") }
            fail("Expected cancellation to propagate")
        } catch (_: CancellationException) {
            // WorkManager must observe cancellation, not a permanent failure result.
        }
    }

    private suspend fun runWorker(recovery: suspend () -> Boolean): ListenableWorker.Result {
        val factory = object : WorkerFactory() {
            override fun createWorker(
                appContext: Context,
                workerClassName: String,
                workerParameters: WorkerParameters,
            ): ListenableWorker? = if (workerClassName == OfflineSubtitleRecoveryWorker::class.java.name) {
                FakeOfflineSubtitleRecoveryWorker(appContext, workerParameters, recovery)
            } else {
                null
            }
        }
        val worker = TestListenableWorkerBuilder<FakeOfflineSubtitleRecoveryWorker>(context)
            .setWorkerFactory(factory)
            .build(OfflineSubtitleRecoveryWorker::class.java)
        return worker.doWork()
    }

    private class FakeOfflineSubtitleRecoveryWorker(
        context: Context,
        parameters: WorkerParameters,
        private val recovery: suspend () -> Boolean,
    ) : OfflineSubtitleRecoveryWorker(context, parameters) {
        override suspend fun recoverPendingSubtitleCaching(): Boolean = recovery()
    }
}
