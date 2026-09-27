package org.mulletaflix.feature.downloads

import org.junit.Assert.assertEquals
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState
import org.junit.Test

class DownloadArtworkModelTest {
    @Test
    fun `offline copy is preferred when the server is unavailable`() {
        val localArtwork = "file:///data/user/0/org.mulletaflix.android/files/offline_artwork/poster.png"
        val entry = entry().copy(offlineArtworkUri = localArtwork)

        assertEquals(localArtwork, downloadArtworkModel(entry, serverUrl = "", accessToken = null))
    }

    @Test
    fun `legacy downloads keep remote artwork fallback`() {
        val entry = entry()

        assertEquals(
            "https://mulletaflix.example/Items/movie-1/Images/Primary?api_key=token",
            downloadArtworkModel(entry, "https://mulletaflix.example", "token"),
        )
    }

    private fun entry() = DownloadEntry(
        id = "movie-1",
        title = "Filme",
        uri = "file:///downloads/movie-1.mp4",
        state = DownloadState.Completed,
        percent = 100,
        imageUrl = "/Items/movie-1/Images/Primary",
    )
}
