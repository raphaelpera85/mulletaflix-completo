package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SubtitleMenuPolicyTest {
    private val tracks = listOf(
        TrackInfo(index = 2, displayName = "Português VTT", isExternal = true, isCastSupported = true),
        TrackInfo(index = 5, displayName = "English"),
        TrackInfo(index = 8, displayName = "Español SRT", isExternal = true),
    )

    @Test
    fun `hides only unsupported external tracks while casting`() {
        assertEquals(listOf(tracks[0], tracks[1]), visibleSubtitleTracks(tracks, isCasting = true))
        assertEquals(tracks, visibleSubtitleTracks(tracks, isCasting = false))
    }

    @Test
    fun `maps selected menu row when Cast hides an unsupported external track`() {
        val visible = visibleSubtitleTracks(tracks, isCasting = true)
        assertEquals(1, visibleSubtitleSelectionIndex(tracks, selectedIndex = 1, visibleTracks = visible))
        assertEquals(0, visibleSubtitleSelectionIndex(tracks, selectedIndex = 0, visibleTracks = visible))
        assertEquals(-1, visibleSubtitleSelectionIndex(tracks, selectedIndex = 2, visibleTracks = visible))
        assertEquals(0, originalSubtitleSelectionIndex(tracks, visible, visibleIndex = 0))
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
    fun `cast subtitle preference ignores unsupported sidecars and keeps supported plus embedded tracks`() {
        val embedded = MediaStream(index = 1, type = MediaStreamType.Subtitle, isExternal = false)
        val vtt = MediaStream(index = 2, type = MediaStreamType.Subtitle, codec = "webvtt", isExternal = true)
        val srt = MediaStream(index = 3, type = MediaStreamType.Subtitle, codec = "srt", isExternal = true)
        assertEquals(
            listOf(embedded, vtt),
            subtitleStreamsForPreferredPlayback(
                listOf(embedded, vtt, srt),
                isCasting = true,
            ),
        )
        assertEquals(
            listOf(embedded, vtt, srt),
            subtitleStreamsForPreferredPlayback(
                listOf(embedded, vtt, srt),
                isCasting = false,
            ),
        )
    }

    @Test
    fun `duplicate external URLs stay available locally but cannot be preferred during Cast`() {
        val embedded = MediaStream(index = 1, type = MediaStreamType.Subtitle, isExternal = false)
        val duplicateOne = MediaStream(
            index = 2,
            type = MediaStreamType.Subtitle,
            codec = "webvtt",
            deliveryUrl = "https://media.example/shared.vtt",
            isExternal = true,
        )
        val unique = MediaStream(
            index = 3,
            type = MediaStreamType.Subtitle,
            codec = "ttml",
            deliveryUrl = "https://media.example/unique.ttml",
            isExternal = true,
        )
        val duplicateTwo = duplicateOne.copy(index = 4, language = "es")
        val streams = listOf(embedded, duplicateOne, unique, duplicateTwo)

        assertEquals(
            listOf(embedded, unique),
            subtitleStreamsForPreferredPlayback(streams, isCasting = true),
        )
        assertEquals(
            streams,
            subtitleStreamsForPreferredPlayback(streams, isCasting = false),
        )
        assertEquals(
            listOf(embedded, unique),
            subtitleStreamsForPreferredPlayback(
                streams = streams,
                isCasting = true,
                castSupportedExternalStreamIndices = setOf(unique.index),
            ),
        )
    }

    @Test
    fun `external sidecar eligibility on Cast requires VTT or TTML and stays online only`() {
        val streams = listOf(
            MediaStream(index = 1, type = MediaStreamType.Subtitle, codec = "webvtt", isExternal = true),
            MediaStream(index = 2, type = MediaStreamType.Subtitle, codec = "ttml", isExternal = true),
            MediaStream(index = 3, type = MediaStreamType.Subtitle, codec = "srt", isExternal = true),
        )

        assertEquals(
            listOf(streams[0], streams[1]),
            externalSubtitleStreamsForPlayback(streams, isCasting = true, isOfflinePlayback = false),
        )
        assertTrue(externalSubtitleStreamsForPlayback(streams, isCasting = true, isOfflinePlayback = true).isEmpty())
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
