package org.mulletaflix.feature.player

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class OsdVisibilityPolicyTest {
    @Test
    fun `phone auto hides osd while playing`() {
        assertTrue(shouldAutoHidePlayerOsd(isTelevision = false, isPlaying = true))
    }

    @Test
    fun `tv keeps osd available while playing`() {
        assertFalse(shouldAutoHidePlayerOsd(isTelevision = true, isPlaying = true))
    }

    @Test
    fun `paused playback does not start auto hide`() {
        assertFalse(shouldAutoHidePlayerOsd(isTelevision = false, isPlaying = false))
    }
}
