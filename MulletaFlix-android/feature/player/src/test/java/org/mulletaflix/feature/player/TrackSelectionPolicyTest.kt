package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class TrackSelectionPolicyTest {

    @Test
    fun `maps global server stream index to filtered audio position`() {
        val tracks = listOf(TrackInfo(2, "Português"), TrackInfo(5, "Inglês"))

        assertEquals(1, uiTrackIndex(tracks, serverStreamIndex = 5, fallback = 0))
    }

    @Test
    fun `uses fallback when server index is absent`() {
        val tracks = listOf(TrackInfo(2, "Português"), TrackInfo(5, "Inglês"))

        assertEquals(0, uiTrackIndex(tracks, serverStreamIndex = null, fallback = 0))
        assertEquals(-1, uiTrackIndex(emptyList(), serverStreamIndex = null, fallback = 0))
    }

    @Test
    fun `maps filtered UI position back to the global server stream index`() {
        val tracks = listOf(TrackInfo(2, "Português"), TrackInfo(5, "Inglês"))

        assertEquals(5, serverTrackIndexAt(tracks, 1))
        assertEquals(null, serverTrackIndexAt(tracks, 2))
    }
}
