package org.mulletaflix.feature.player

import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class PlayerFallbackPolicyTest {
    @Test
    fun `fallback preserves a valid playback position`() {
        assertEquals(42_000L, fallbackPosition(42_000L))
        assertEquals(0L, fallbackPosition(-1L))
    }

    @Test
    fun `falls back once when direct stream fails`() {
        assertTrue(
            shouldFallbackToTranscode(
                currentUri = "http://server/direct",
                transcodeUri = "http://server/master.m3u8",
                alreadyTried = false,
            )
        )
    }

    @Test
    fun `does not loop after fallback or when url is unavailable`() {
        assertFalse(shouldFallbackToTranscode("http://server/direct", "http://server/master.m3u8", true))
        assertFalse(shouldFallbackToTranscode("http://server/master.m3u8", "http://server/master.m3u8", false))
        assertFalse(shouldFallbackToTranscode("http://server/direct", null, false))
    }
}
