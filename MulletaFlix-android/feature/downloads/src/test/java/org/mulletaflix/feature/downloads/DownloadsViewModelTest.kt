package org.mulletaflix.feature.downloads

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.After
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class DownloadsViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun `downloads return to loading on initial and resumed collection until a fresh snapshot arrives`() = runTest(dispatcher) {
        var snapshotCount = 0
        val repository = FakeDownloadRepository().apply {
            downloadsFlow = flow {
                val snapshot = ++snapshotCount
                delay(250)
                emit(
                    if (snapshot == 1) emptyList()
                    else listOf(
                        DownloadEntry(
                            "movie",
                            "Filme",
                            "https://server/movie",
                            org.mulletaflix.domain.repository.DownloadState.Completed,
                            100,
                        ),
                    ),
                )
                awaitCancellation()
            }
        }
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))
        val collector = backgroundScope.launch(UnconfinedTestDispatcher(dispatcher.scheduler)) {
            viewModel.downloadsState.collect {}
        }

        assertFalse(viewModel.downloadsState.value.isLoaded)
        assertEquals(
            DownloadsContentState.Loading,
            downloadsContentState(viewModel.downloadsState.value.isLoaded, viewModel.downloadsState.value.entries.size),
        )
        advanceTimeBy(249)
        runCurrent()
        assertFalse(viewModel.downloadsState.value.isLoaded)

        advanceTimeBy(1)
        runCurrent()
        assertTrue(viewModel.downloadsState.value.isLoaded)
        assertEquals(DownloadsContentState.Empty, downloadsContentState(true, 0))
        collector.cancel()

        advanceTimeBy(5_000)
        runCurrent()
        assertFalse("o snapshot não deve ser reapresentado após a coleta expirar", viewModel.downloadsState.value.isLoaded)
        assertEquals(DownloadsContentState.Loading, downloadsContentState(viewModel.downloadsState.value.isLoaded, 0))

        val resumedCollector = backgroundScope.launch(UnconfinedTestDispatcher(dispatcher.scheduler)) {
            viewModel.downloadsState.collect {}
        }
        runCurrent()
        assertEquals(2, snapshotCount)
        assertFalse(viewModel.downloadsState.value.isLoaded)
        advanceTimeBy(249)
        runCurrent()
        assertFalse(viewModel.downloadsState.value.isLoaded)

        advanceTimeBy(1)
        runCurrent()
        assertTrue(viewModel.downloadsState.value.isLoaded)
        assertEquals(DownloadsContentState.Content, downloadsContentState(true, 1))
        resumedCollector.cancel()
    }

    @Test
    fun `downloads content state separates loading empty and populated queue`() {
        assertEquals(DownloadsContentState.Loading, downloadsContentState(isLoaded = false, itemCount = 0))
        assertEquals(DownloadsContentState.Empty, downloadsContentState(isLoaded = true, itemCount = 0))
        assertEquals(DownloadsContentState.Content, downloadsContentState(isLoaded = true, itemCount = 1))
    }

    @Test
    fun `downloads content width adapts to phone tablet and tv`() {
        assertEquals(411, downloadsContentMaxWidthDp(411, isTelevision = false))
        assertEquals(960, downloadsContentMaxWidthDp(800, isTelevision = false))
        assertEquals(1200, downloadsContentMaxWidthDp(1920, isTelevision = true))
    }

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
    fun `failed retry exposes a recoverable action message`() {
        val repository = FakeDownloadRepository(failRetry = true)
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))
        val entry = DownloadEntry(
            "movie",
            "Filme",
            "https://server/media",
            org.mulletaflix.domain.repository.DownloadState.Failed,
            42,
        )

        viewModel.retry(entry)

        assertEquals("retry failed", viewModel.actionMessage.value)
        viewModel.clearActionMessage()
        assertEquals(null, viewModel.actionMessage.value)
    }

    @Test
    fun `failed wifi preference exposes an action message`() {
        val repository = FakeDownloadRepository(failWifiOnly = true)
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))

        viewModel.setWifiOnly(true)

        assertEquals("wifi preference failed", viewModel.actionMessage.value)
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

    @Test
    fun `retry failed requeues every failed item in queue order`() {
        val repository = FakeDownloadRepository()
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))
        val entries = listOf(
            DownloadEntry("queued", "Na fila", "https://server/queued", org.mulletaflix.domain.repository.DownloadState.Queued, 0),
            DownloadEntry("failed-1", "Falhou 1", "https://server/one", org.mulletaflix.domain.repository.DownloadState.Failed, 20),
            DownloadEntry("done", "Pronto", "https://server/done", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
            DownloadEntry("failed-2", "Falhou 2", "https://server/two", org.mulletaflix.domain.repository.DownloadState.Failed, 5),
        )

        viewModel.retryFailed(entries)

        assertEquals(listOf("failed-1", "failed-2"), repository.retriedIds)
    }

    @Test
    fun `completed download policy excludes active and failed items`() {
        val entries = listOf(
            DownloadEntry("done", "Pronto", "https://server/done", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
            DownloadEntry("active", "Baixando", "https://server/active", org.mulletaflix.domain.repository.DownloadState.Downloading, 50),
            DownloadEntry("failed", "Falhou", "https://server/failed", org.mulletaflix.domain.repository.DownloadState.Failed, 10),
        )

        assertEquals(listOf("done"), completedDownloads(entries).map { it.id })
    }

    @Test
    fun `failed download policy excludes active and completed items`() {
        val entries = listOf(
            DownloadEntry("done", "Pronto", "https://server/done", org.mulletaflix.domain.repository.DownloadState.Completed, 100),
            DownloadEntry("active", "Baixando", "https://server/active", org.mulletaflix.domain.repository.DownloadState.Downloading, 50),
            DownloadEntry("failed", "Falhou", "https://server/failed", org.mulletaflix.domain.repository.DownloadState.Failed, 10),
        )

        assertEquals(listOf("failed"), failedDownloads(entries).map { it.id })
    }

    @Test
    fun `failed download policy removes duplicate ids while preserving first occurrence`() {
        val entries = listOf(
            DownloadEntry("failed", "Falhou primeiro", "https://server/one", org.mulletaflix.domain.repository.DownloadState.Failed, 10),
            DownloadEntry("failed", "Falhou repetido", "https://server/one", org.mulletaflix.domain.repository.DownloadState.Failed, 20),
            DownloadEntry("other", "Outra falha", "https://server/two", org.mulletaflix.domain.repository.DownloadState.Failed, 5),
        )

        assertEquals(listOf("failed", "other"), failedDownloads(entries).map { it.id })
        assertEquals("Falhou primeiro", failedDownloads(entries).first().title)
    }

    @Test
    fun `clear completed delegates to repository`() {
        val repository = FakeDownloadRepository()
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))

        viewModel.removeCompleted()

        assertTrue(repository.removedCompleted)
    }

    @Test
    fun `clear failed delegates to repository`() {
        val repository = FakeDownloadRepository()
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))

        viewModel.removeFailed()

        assertTrue(repository.removedFailed)
    }

    private class FakeDownloadRepository(
        private val failPause: Boolean = false,
        private val failRetry: Boolean = false,
        private val failWifiOnly: Boolean = false,
    ) : DownloadRepository {
        var downloadsFlow: Flow<List<DownloadEntry>> = flowOf(emptyList())
        var paused = false
        var resumed = false
        var retried = false
        var retryArguments = emptyArray<String>()
        val retriedIds = mutableListOf<String>()
        var wifiOnly = false
        var removedCompleted = false
        var removedFailed = false
        private val queuePaused = MutableStateFlow(false)

        override fun observeDownloads(): Flow<List<DownloadEntry>> = downloadsFlow
        override fun observeQueuePaused(): Flow<Boolean> = queuePaused
        override fun observeWifiOnly(): Flow<Boolean> = flowOf(wifiOnly)
        override fun setWifiOnly(enabled: Boolean): Result<Unit> {
            if (failWifiOnly) return Result.failure(IllegalStateException("wifi preference failed"))
            wifiOnly = enabled
            return Result.success(Unit)
        }
        override fun enqueue(id: String, title: String, uri: String): Result<Unit> = Result.success(Unit)
        override fun retry(id: String, title: String, uri: String): Result<Unit> {
            if (failRetry) return Result.failure(IllegalStateException("retry failed"))
            retried = true
            retryArguments = arrayOf(id, title, uri)
            retriedIds += id
            return Result.success(Unit)
        }
        override fun remove(id: String): Result<Unit> = Result.success(Unit)
        override fun removeCompleted(): Result<Unit> {
            removedCompleted = true
            return Result.success(Unit)
        }
        override fun removeFailed(): Result<Unit> {
            removedFailed = true
            return Result.success(Unit)
        }
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
