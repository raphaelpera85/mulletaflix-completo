package org.mulletaflix.feature.player

import androidx.media3.common.C
import com.google.android.gms.cast.MediaTrack
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MulletaFlixCastTrackSelectorTest {
    private val english = CastTrackCandidate(groupIndex = 0, type = MediaTrack.TYPE_TEXT, language = "en")
    private val portuguese = CastTrackCandidate(groupIndex = 1, type = MediaTrack.TYPE_TEXT, language = "pt")

    @Test
    fun `uses explicit sender override over receiver current selection`() {
        assertEquals(
            setOf(portuguese.groupIndex),
            selectCastTrackGroupIndices(
                candidates = listOf(english, portuguese),
                currentlySelectedIndices = setOf(english.groupIndex),
                explicitOverrides = mapOf(portuguese.groupIndex to true),
                disabledTrackTypes = emptySet(),
                preferredLanguages = emptyMap(),
            ),
        )
    }

    @Test
    fun `honors preferred text language when no receiver track is active`() {
        assertEquals(
            setOf(portuguese.groupIndex),
            selectCastTrackGroupIndices(
                candidates = listOf(english, portuguese),
                currentlySelectedIndices = emptySet(),
                explicitOverrides = emptyMap(),
                disabledTrackTypes = emptySet(),
                preferredLanguages = mapOf(3 to listOf("pt-BR")),
            ),
        )
    }

    @Test
    fun `does not select text when disabled and preserves receiver choice otherwise`() {
        assertEquals(
            setOf(english.groupIndex),
            selectCastTrackGroupIndices(
                candidates = listOf(english, portuguese),
                currentlySelectedIndices = setOf(english.groupIndex),
                explicitOverrides = emptyMap(),
                disabledTrackTypes = emptySet(),
                preferredLanguages = emptyMap(),
            ),
        )

        assertTrue(
            selectCastTrackGroupIndices(
                candidates = listOf(english, portuguese),
                currentlySelectedIndices = setOf(english.groupIndex),
                explicitOverrides = emptyMap(),
                disabledTrackTypes = setOf(C.TRACK_TYPE_TEXT),
                preferredLanguages = emptyMap(),
            ).isEmpty(),
        )
    }

    @Test
    fun `does not claim audio switching with the default receiver`() {
        val audio = CastTrackCandidate(groupIndex = 0, type = MediaTrack.TYPE_AUDIO, language = "pt")
        assertTrue(
            selectCastTrackGroupIndices(
                candidates = listOf(audio),
                currentlySelectedIndices = emptySet(),
                explicitOverrides = emptyMap(),
                disabledTrackTypes = emptySet(),
                preferredLanguages = emptyMap(),
            ).isEmpty(),
        )
    }
}
