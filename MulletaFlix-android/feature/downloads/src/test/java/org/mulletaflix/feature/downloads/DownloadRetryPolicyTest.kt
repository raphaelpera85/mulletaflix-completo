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
}
