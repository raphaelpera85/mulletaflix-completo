package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A download must be able to switch its own audio and subtitle tracks.
 *
 * Offline there is no server metadata, so `audioTracks`/`subtitleTracks` stayed
 * empty — and the OSD enables its audio and subtitle buttons from those lists, so
 * a downloaded file carrying several tracks could not have any of them switched.
 * The buttons were disabled by missing state, not by missing content.
 */
class OfflineTrackPolicyTest {

    private val twoAudioAndOneSubtitle = listOf(
        OfflineTrack(language = "por", codec = "audio/mp4a-latm", channels = 2, isSelected = true),
        OfflineTrack(language = "eng", codec = "audio/ac-3", channels = 6),
    )

    @Test
    fun `offline tracks are listed with a position as their identity`() {
        val audio = offlineTrackInfos(twoAudioAndOneSubtitle, "Áudio")

        assertEquals(2, audio.size)
        assertEquals(
            "offline the position is the index, which the selection path resolves by lookup",
            listOf(0, 1),
            audio.map { it.index },
        )
    }

    @Test
    fun `offline labels come from the container and fall back to a number`() {
        val audio = offlineTrackInfos(twoAudioAndOneSubtitle, "Áudio")

        assertEquals("Português", audio[0].displayName)
        assertEquals("English", audio[1].displayName)
        assertEquals("MP4A-LATM", trackLabel(audio[0]).substringAfter(" • ").substringBefore(" • "))
        assertTrue("the channel layout must be described", trackLabel(audio[1]).contains("5.1"))

        val unnamed = offlineTrackInfos(listOf(OfflineTrack(), OfflineTrack()), "Legenda")
        assertEquals("Legenda 1", unnamed[0].displayName)
        assertEquals("Legenda 2", unnamed[1].displayName)
    }

    @Test
    fun `the index list makes the lookup the identity`() {
        val indices = offlineStreamIndices(3)

        assertEquals(listOf(0, 1, 2), indices)
        assertEquals(0, trackCandidatePosition(0, indices))
        assertEquals(2, trackCandidatePosition(2, indices))
    }

    @Test
    fun `the track the player is already using is the one marked`() {
        assertEquals(0, selectedOfflineTrackIndex(twoAudioAndOneSubtitle))
    }

    @Test
    fun `nothing is marked when the player reported no selection`() {
        val none = listOf(OfflineTrack(language = "por"), OfflineTrack(language = "eng"))

        assertEquals(-1, selectedOfflineTrackIndex(none))
        assertEquals(-1, selectedOfflineTrackIndex(emptyList()))
    }
}
