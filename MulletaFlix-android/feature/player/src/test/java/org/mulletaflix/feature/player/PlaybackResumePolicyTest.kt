package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class PlaybackResumePolicyTest {
    @Test
    fun `server progress wins over local fallback`() {
        assertEquals(120_000L, chooseResumePositionMs(120_000L, 240_000L))
    }

    @Test
    fun `local progress fills missing server progress`() {
        assertEquals(240_000L, chooseResumePositionMs(null, 240_000L))
        assertEquals(240_000L, chooseResumePositionMs(0L, 240_000L))
    }

    @Test
    fun `invalid progress starts at the beginning`() {
        assertEquals(0L, chooseResumePositionMs(null, null))
        assertEquals(0L, chooseResumePositionMs(0L, 0L))
        assertEquals(0L, chooseResumePositionMs(-1L, -1L))
    }
}
