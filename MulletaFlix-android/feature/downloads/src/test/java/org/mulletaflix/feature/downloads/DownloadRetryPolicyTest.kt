package org.mulletaflix.feature.downloads

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

class DownloadRetryPolicyTest {

    @Test
    fun `selects only failed entries and preserves queue order`() {
        val downloads = listOf(
            DownloadEntry("queued", "Na fila", "https://server/queued", DownloadState.Queued, 0),
            DownloadEntry("failed-1", "Falhou 1", "https://server/one", DownloadState.Failed, 20),
            DownloadEntry("done", "Pronto", "https://server/done", DownloadState.Completed, 100),
            DownloadEntry("failed-2", "Falhou 2", "https://server/two", DownloadState.Failed, 5),
        )

        assertEquals(listOf("failed-1", "failed-2"), failedDownloads(downloads).map { it.id })
    }

    @Test
    fun `same failed media id on two servers remains independently retryable`() {
        val downloads = listOf(
            DownloadEntry("movie", "Filme A", "https://a/movie", DownloadState.Failed, 20, downloadId = "a", serverId = "server-a"),
            DownloadEntry("movie", "Filme B", "https://b/movie", DownloadState.Failed, 10, downloadId = "b", serverId = "server-b"),
        )

        assertEquals(listOf("a", "b"), failedDownloads(downloads).map { it.downloadId })
    }
}
