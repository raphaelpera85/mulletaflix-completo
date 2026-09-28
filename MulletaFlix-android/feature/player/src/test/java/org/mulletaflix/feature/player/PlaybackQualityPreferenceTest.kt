package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackQualityPreferenceTest {
    @Test
    fun `manual selection survives a stale stored preference emission`() {
        val selected = PlaybackQualityPreference().select("1080p")
        val afterStaleEmission = selected.applyStoredPreference("Auto")

        assertEquals("1080p", afterStaleEmission.quality)
        assertFalse(afterStaleEmission.shouldApplyMeteredAutoCap)
    }

    @Test
    fun `automatic selection keeps metered network cap enabled`() {
        val selected = PlaybackQualityPreference("1080p").select("Auto")

        assertEquals("Auto", selected.quality)
        assertTrue(selected.shouldApplyMeteredAutoCap)
    }

    @Test
    fun `stored preference initializes the choice before user interaction`() {
        val preference = PlaybackQualityPreference().applyStoredPreference(" 720p ")

        assertEquals("720p", preference.quality)
        assertFalse(preference.shouldApplyMeteredAutoCap)
    }
}
