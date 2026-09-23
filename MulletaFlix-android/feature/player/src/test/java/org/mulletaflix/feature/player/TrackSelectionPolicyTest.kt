package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
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

    /**
     * The server's index is not the container's track identity.
     *
     * Jellyfin's `Index` is 0-based across every stream of the file, while
     * `Format.id` is the container's own name for the track — the EBML
     * `TrackNumber` for Matroska, which is 1-based. Comparing them directly played
     * the wrong track, and the old fallback compared the server index with a
     * group-local track index, a third numbering space entirely.
     */
    @Test
    fun `a server index resolves to its position among the tracks of its type`() {
        // A dual-audio MKV: Jellyfin lists video=0, audio=1, audio=2.
        val audioServerIndices = orderedStreamIndices(listOf(2, 1))

        assertEquals(listOf(1, 2), audioServerIndices)
        assertEquals("server index 1 is the first audio track", 0, trackCandidatePosition(1, audioServerIndices))
        assertEquals("server index 2 is the second audio track", 1, trackCandidatePosition(2, audioServerIndices))
    }

    @Test
    fun `the server index is never taken for a container id or a raw position`() {
        val audioServerIndices = orderedStreamIndices(listOf(1, 2))

        // Container ids for that file are "2" and "3", so server index 2 must not
        // be resolved to the first audio track.
        assertNotEquals(
            "server index 2 is not the first audio track",
            0,
            trackCandidatePosition(2, audioServerIndices),
        )
    }

    @Test
    fun `an index that is not among the tracks resolves to nothing`() {
        // Declining is right: guessing is what played an unrelated track.
        assertNull(trackCandidatePosition(9, listOf(1, 2)))
        assertNull(trackCandidatePosition(1, emptyList()))
    }

    @Test
    fun `ordering does not depend on the order the server serialised`() {
        assertEquals(listOf(1, 2, 3), orderedStreamIndices(listOf(3, 1, 2)))
    }
}
