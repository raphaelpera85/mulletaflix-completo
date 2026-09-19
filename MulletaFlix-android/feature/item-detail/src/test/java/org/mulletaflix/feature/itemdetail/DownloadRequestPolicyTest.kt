package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

class DownloadRequestPolicyTest {
    @Test
    fun `queued, downloading and completed entries block duplicates`() {
        listOf(DownloadState.Queued, DownloadState.Downloading, DownloadState.Completed).forEach { state ->
            assertTrue(hasActiveDownload(listOf(entry(state)), "movie-1"))
        }
    }

    @Test
    fun `failed and removing entries can be requested again`() {
        assertFalse(hasActiveDownload(listOf(entry(DownloadState.Failed)), "movie-1"))
        assertFalse(hasActiveDownload(listOf(entry(DownloadState.Removing)), "movie-1"))
    }

    @Test
    fun `different media never blocks the requested item`() {
        assertFalse(hasActiveDownload(listOf(entry(DownloadState.Completed, id = "movie-2")), "movie-1"))
    }

    private fun entry(state: DownloadState, id: String = "movie-1") = DownloadEntry(
        id = id,
        title = "Movie",
        uri = "https://example.test/movie.mp4",
        state = state,
        percent = 100,
    )
}
