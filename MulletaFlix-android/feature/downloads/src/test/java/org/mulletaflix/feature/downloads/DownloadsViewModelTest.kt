package org.mulletaflix.feature.downloads

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase

class DownloadsViewModelTest {
    @Test
    fun `pausing the queue updates state only after repository succeeds`() {
        val repository = FakeDownloadRepository()
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))

        viewModel.pauseQueue()

        assertTrue(repository.paused)
        assertTrue(viewModel.queuePaused.value)
    }

    @Test
    fun `failed queue operation does not lie about paused state`() {
        val repository = FakeDownloadRepository(failPause = true)
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))

        viewModel.pauseQueue()

        assertFalse(viewModel.queuePaused.value)
    }

    @Test
    fun `resuming the queue clears paused state after repository succeeds`() {
        val repository = FakeDownloadRepository()
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))
        viewModel.pauseQueue()

        viewModel.resumeQueue()

        assertTrue(repository.resumed)
        assertFalse(viewModel.queuePaused.value)
    }

    @Test
    fun `wifi only preference delegates to repository`() {
        val repository = FakeDownloadRepository()
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))

        viewModel.setWifiOnly(true)

        assertTrue(repository.wifiOnly)
    }

    @Test
    fun `retry delegates the failed entry with its original metadata`() {
        val repository = FakeDownloadRepository()
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))
        val entry = DownloadEntry("movie", "Filme", "https://server/media", org.mulletaflix.domain.repository.DownloadState.Failed, 42)

        viewModel.retry(entry)

        assertTrue(repository.retried)
        assertTrue(repository.retryArguments.contentEquals(arrayOf("movie", "Filme", "https://server/media")))
    }

    private class FakeDownloadRepository(
        private val failPause: Boolean = false,
    ) : DownloadRepository {
        var paused = false
        var resumed = false
        var retried = false
        var retryArguments = emptyArray<String>()
        var wifiOnly = false

        override fun observeDownloads(): Flow<List<DownloadEntry>> = flowOf(emptyList())
        override fun observeWifiOnly(): Flow<Boolean> = flowOf(wifiOnly)
        override fun setWifiOnly(enabled: Boolean): Result<Unit> {
            wifiOnly = enabled
            return Result.success(Unit)
        }
        override fun enqueue(id: String, title: String, uri: String): Result<Unit> = Result.success(Unit)
        override fun retry(id: String, title: String, uri: String): Result<Unit> {
            retried = true
            retryArguments = arrayOf(id, title, uri)
            return Result.success(Unit)
        }
        override fun remove(id: String): Result<Unit> = Result.success(Unit)
        override fun pauseAll(): Result<Unit> = if (failPause) {
            Result.failure(IllegalStateException("pause failed"))
        } else {
            paused = true
            Result.success(Unit)
        }
        override fun resumeAll(): Result<Unit> {
            resumed = true
            paused = false
            return Result.success(Unit)
        }
    }
}
