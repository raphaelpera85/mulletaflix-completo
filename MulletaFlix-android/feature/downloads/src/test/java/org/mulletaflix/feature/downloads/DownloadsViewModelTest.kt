package org.mulletaflix.feature.downloads

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase

class DownloadsViewModelTest {
    @Test
    fun `download search is case insensitive and keeps repository order`() {
        val downloads = listOf(
            DownloadEntry("1", "A Viagem", "uri-1", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
            DownloadEntry("2", "O Retorno", "uri-2", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
            DownloadEntry("3", "Viagem ao Centro", "uri-3", org.mulletaflix.domain.repository.DownloadState.Queued, 10),
        )

        assertEquals(listOf("1", "3"), filterDownloads(downloads, "viagem").map { it.id })
    }

    @Test
    fun `blank download search returns the complete queue`() {
        val downloads = listOf(
            DownloadEntry("1", "A Viagem", "uri-1", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
        )

        assertEquals(downloads, filterDownloads(downloads, "   "))
    }

    @Test
    fun `status filter keeps only matching downloads and preserves order`() {
        val downloads = listOf(
            DownloadEntry("1", "Baixando", "uri-1", org.mulletaflix.domain.repository.DownloadState.Downloading, 50),
            DownloadEntry("2", "Pronto", "uri-2", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
            DownloadEntry("3", "Na fila", "uri-3", org.mulletaflix.domain.repository.DownloadState.Queued, 0),
            DownloadEntry("4", "Falhou", "uri-4", org.mulletaflix.domain.repository.DownloadState.Failed, 20),
        )

        assertEquals(listOf("1", "3"), filterDownloads(downloads, "", DownloadStatusFilter.InProgress).map { it.id })
        assertEquals(listOf("4"), filterDownloads(downloads, "", DownloadStatusFilter.Failed).map { it.id })
    }

    @Test
    fun `status filter combines with title search`() {
        val downloads = listOf(
            DownloadEntry("1", "Viagem pronta", "uri-1", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
            DownloadEntry("2", "Viagem falhou", "uri-2", org.mulletaflix.domain.repository.DownloadState.Failed, 20),
        )

        assertEquals(
            listOf("1"),
            filterDownloads(downloads, "viagem", DownloadStatusFilter.Completed).map { it.id },
        )
    }

    @Test
    fun `storage summary aggregates downloaded and known content bytes`() {
        val downloads = listOf(
            DownloadEntry("1", "Filme", "uri-1", org.mulletaflix.domain.repository.DownloadState.Completed, 100, bytesDownloaded = 2_000_000, contentLength = 2_500_000),
            DownloadEntry("2", "Série", "uri-2", org.mulletaflix.domain.repository.DownloadState.Downloading, 50, bytesDownloaded = 500_000),
        )

        assertEquals(2_500_000L, summarizeDownloadStorage(downloads).downloadedBytes)
        assertEquals(2_500_000L, summarizeDownloadStorage(downloads).knownContentBytes)
        assertEquals(2, summarizeDownloadStorage(downloads).itemCount)
    }

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
        private val queuePaused = MutableStateFlow(false)

        override fun observeDownloads(): Flow<List<DownloadEntry>> = flowOf(emptyList())
        override fun observeQueuePaused(): Flow<Boolean> = queuePaused
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
            queuePaused.value = true
            Result.success(Unit)
        }
        override fun resumeAll(): Result<Unit> {
            resumed = true
            paused = false
            queuePaused.value = false
            return Result.success(Unit)
        }
    }
}
