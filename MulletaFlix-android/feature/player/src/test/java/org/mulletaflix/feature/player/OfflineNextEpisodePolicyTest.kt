package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import org.mulletaflix.domain.repository.DownloadState

class OfflineNextEpisodePolicyTest {
    private fun episode(
        id: String,
        series: String = "series-a",
        season: Int,
        number: Int,
        state: DownloadState = DownloadState.Completed,
    ) = DownloadEntry(
        id = id,
        title = id,
        uri = "file:///$id",
        state = state,
        percent = if (state == DownloadState.Completed) 100 else 40,
        episodeMetadata = DownloadEpisodeMetadata(series, season, number),
    )

    @Test
    fun `chooses next completed episode in season and skips unfinished downloads`() {
        val current = episode("s1e1", season = 1, number = 1)
        val expected = episode("s1e3", season = 1, number = 3)

        assertEquals(
            expected,
            nextCompletedDownloadedEpisode(
                current,
                listOf(
                    episode("s1e2", season = 1, number = 2, state = DownloadState.Downloading),
                    episode("s2e1", season = 2, number = 1),
                    expected,
                    current,
                ),
            ),
        )
    }

    @Test
    fun `moves to earliest completed episode in a later season`() {
        val current = episode("s1e8", season = 1, number = 8)

        assertEquals(
            "s2e1",
            nextCompletedDownloadedEpisode(
                current,
                listOf(episode("s3e1", season = 3, number = 1), episode("s2e1", season = 2, number = 1)),
            )?.id,
        )
    }

    @Test
    fun `does not select another series or the current episode`() {
        val current = episode("s1e1", season = 1, number = 1)

        assertNull(
            nextCompletedDownloadedEpisode(
                current,
                listOf(current, episode("other", series = "series-b", season = 1, number = 2)),
            ),
        )
    }

    @Test
    fun `does not offer an episode unless current download is complete`() {
        val current = episode("s1e1", season = 1, number = 1, state = DownloadState.Downloading)

        assertNull(nextCompletedDownloadedEpisode(current, listOf(episode("s1e2", season = 1, number = 2))))
    }
}
