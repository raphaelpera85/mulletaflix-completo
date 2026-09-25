package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SubtitleMenuPolicyTest {
    private val tracks = listOf(
        TrackInfo(index = 2, displayName = "Português", isExternal = true),
        TrackInfo(index = 5, displayName = "English"),
        TrackInfo(index = 8, displayName = "Español", isExternal = true),
    )

    @Test
    fun `hides only external tracks while casting`() {
        assertEquals(listOf(tracks[1]), visibleSubtitleTracks(tracks, isCasting = true))
        assertEquals(tracks, visibleSubtitleTracks(tracks, isCasting = false))
    }

    @Test
    fun `maps selected menu row when cast hides preceding external track`() {
        val visible = visibleSubtitleTracks(tracks, isCasting = true)
        assertEquals(0, visibleSubtitleSelectionIndex(tracks, selectedIndex = 1, visibleTracks = visible))
        assertEquals(-1, visibleSubtitleSelectionIndex(tracks, selectedIndex = 0, visibleTracks = visible))
        assertEquals(1, originalSubtitleSelectionIndex(tracks, visible, visibleIndex = 0))
        assertNull(originalSubtitleSelectionIndex(tracks, visible, visibleIndex = -1))
    }

    @Test
    fun `selects preferred external subtitle only for online local playback`() {
        val subtitle = MediaStream(
            index = 8,
            type = MediaStreamType.Subtitle,
            codec = "srt",
            isExternal = true,
        )
        val streams = externalSubtitleStreamsForPlayback(
            streams = listOf(subtitle),
            isCasting = false,
            isOfflinePlayback = false,
        )

        assertEquals(listOf(subtitle), streams)
        assertEquals(
            subtitle,
            selectedExternalSubtitleStream(
                streams = streams,
                preferredStreamIndex = 8,
            ),
        )
        assertTrue(
            externalSubtitleStreamsForPlayback(
                streams = listOf(subtitle),
                isCasting = true,
                isOfflinePlayback = false,
            ).isEmpty(),
        )
        assertTrue(
            externalSubtitleStreamsForPlayback(
                streams = listOf(subtitle),
                isCasting = false,
                isOfflinePlayback = true,
            ).isEmpty(),
        )
        assertNull(selectedExternalSubtitleStream(emptyList(), preferredStreamIndex = 8))
    }

    @Test
    fun `cast subtitle preference ignores external sidecars but keeps embedded tracks`() {
        val embedded = MediaStream(index = 1, type = MediaStreamType.Subtitle, isExternal = false)
        val external = MediaStream(index = 2, type = MediaStreamType.Subtitle, isExternal = true)
        assertEquals(
            listOf(embedded),
            subtitleStreamsForPreferredPlayback(
                listOf(embedded, external),
                isCasting = true,
            ),
        )
        assertEquals(
            listOf(embedded, external),
            subtitleStreamsForPreferredPlayback(
                listOf(embedded, external),
                isCasting = false,
            ),
        )
    }

    @Test
    fun `does not treat embedded or unselected streams as external sidecars`() {
        val streams = listOf(
            MediaStream(index = 8, type = MediaStreamType.Subtitle, isExternal = false),
            MediaStream(index = 9, type = MediaStreamType.Audio, isExternal = true),
        )

        val playableStreams = externalSubtitleStreamsForPlayback(
            streams = streams,
            isCasting = false,
            isOfflinePlayback = false,
        )
        assertTrue(playableStreams.isEmpty())
        assertNull(selectedExternalSubtitleStream(playableStreams, preferredStreamIndex = 8))
        assertNull(selectedExternalSubtitleStream(playableStreams, preferredStreamIndex = 9))
        assertNull(
            selectedExternalSubtitleStream(
                streams = playableStreams,
                preferredStreamIndex = null,
            ),
        )
    }
}
