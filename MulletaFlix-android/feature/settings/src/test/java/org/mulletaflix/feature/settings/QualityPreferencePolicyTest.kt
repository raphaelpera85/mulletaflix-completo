package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Test

class QualityPreferencePolicyTest {

    @Test
    fun `exposes player quality options including 4K and 1440p`() {
        assertEquals(listOf("Auto", "4K", "1440p", "1080p", "720p", "480p"), defaultQualityChoices)
    }

    @Test
    fun `normalizes valid values and falls back corrupted values to auto`() {
        assertEquals("4K", normalizeDefaultQuality("4k"))
        assertEquals("1440p", normalizeDefaultQuality(" 1440P "))
        assertEquals("Auto", normalizeDefaultQuality("unsupported"))
        assertEquals("Auto", normalizeDefaultQuality(null))
    }
}
