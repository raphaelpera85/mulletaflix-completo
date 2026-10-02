package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.UserMediaPreferenceScope

class TrackPreferenceScopePolicyTest {
    private val accountScope = UserMediaPreferenceScope(
        userId = "user-1",
        serverId = "server-1",
        serverUrl = "http://server.test",
    )

    @Test
    fun `episode with series identity receives series override scope`() {
        val episode = MediaItem(
            id = "episode-1",
            name = "Episódio",
            type = MediaItemType.Episode,
            seriesId = " series-1 ",
        )

        assertEquals("series-1", accountScope.forPlaybackItem(episode).seriesId)
        assertEquals(true, accountScope.forPlaybackItem(episode).isEpisode)
        assertEquals("user-1", accountScope.forPlaybackItem(episode).userId)
        assertEquals("server-1", accountScope.forPlaybackItem(episode).serverId)
    }

    @Test
    fun `movie and episode without valid series identity retain account scope`() {
        val movie = MediaItem(
            id = "movie-1",
            name = "Filme",
            type = MediaItemType.Movie,
            seriesId = "series-1",
        )
        val missingSeries = MediaItem(
            id = "episode-2",
            name = "Episódio sem série",
            type = MediaItemType.Episode,
            seriesId = "  ",
        )
        val tooLong = MediaItem(
            id = "episode-3",
            name = "Série inválida",
            type = MediaItemType.Episode,
            seriesId = "x".repeat(129),
        )

        assertEquals(null, accountScope.forPlaybackItem(movie).seriesId)
        assertEquals(null, accountScope.forPlaybackItem(missingSeries).seriesId)
        assertEquals(null, accountScope.forPlaybackItem(tooLong).seriesId)
        assertEquals(true, accountScope.forPlaybackItem(tooLong).isEpisode)
        assertEquals(accountScope, accountScope.forPlaybackItem(movie))
    }

    @Test
    fun `invalid offline episode identity remains episode scoped without series override`() {
        val scope = accountScope.forEpisodeSeries("x".repeat(129))

        assertEquals(true, scope.isEpisode)
        assertEquals(null, scope.seriesId)
        assertEquals("user-1", scope.userId)
    }
}
