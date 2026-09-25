package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class AutomaticIntroSkipPolicyTest {
    private val intro = ChapterSkipAction(ChapterSkipKind.INTRO, targetPositionMs = 90_000L)

    @Test
    fun `does not skip until the user opts in`() {
        assertNull(automaticIntroSkipTarget(false, true, true, 30_000L, intro, null))
    }

    @Test
    fun `skips detected intro only during active seekable playback`() {
        assertEquals(90_000L, automaticIntroSkipTarget(true, true, true, 30_000L, intro, null))
        assertNull(automaticIntroSkipTarget(true, false, true, 30_000L, intro, null))
        assertNull(automaticIntroSkipTarget(true, true, false, 30_000L, intro, null))
    }

    @Test
    fun `does not auto-skip credits or repeat a pending seek`() {
        val credits = ChapterSkipAction(ChapterSkipKind.CREDITS, targetPositionMs = 120_000L)
        assertNull(automaticIntroSkipTarget(true, true, true, 30_000L, credits, null))
        assertNull(automaticIntroSkipTarget(true, true, true, 30_000L, intro, 90_000L))
    }

    @Test
    fun `does not seek when target is at or behind the current position`() {
        assertNull(automaticIntroSkipTarget(true, true, true, 90_000L, intro, null))
    }
}
