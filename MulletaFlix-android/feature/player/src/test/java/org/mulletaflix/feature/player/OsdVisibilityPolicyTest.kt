package org.mulletaflix.feature.player

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import androidx.compose.ui.input.key.Key
import org.junit.Test

class OsdVisibilityPolicyTest {
    @Test
    fun `phone auto hides osd while playing`() {
        assertTrue(shouldAutoHidePlayerOsd(isPlaying = true))
    }

    @Test
    fun `tv auto hides osd while playing`() {
        assertTrue(shouldAutoHidePlayerOsd(isPlaying = true))
    }

    @Test
    fun `tv keeps auto hide armed after playback has started`() {
        assertTrue(shouldAutoHidePlayerOsd(isPlaying = false, tvPlaybackHasStarted = true))
    }

    @Test
    fun `paused playback does not start auto hide`() {
        assertFalse(shouldAutoHidePlayerOsd(isPlaying = false))
    }

    @Test
    fun `tv pause before playback starts does not arm auto hide`() {
        assertFalse(shouldAutoHidePlayerOsd(isPlaying = false, tvPlaybackHasStarted = false))
    }

    @Test
    fun `direction and select keys can reveal hidden tv controls`() {
        assertTrue(isPlayerOsdRevealKey(Key.DirectionDown))
        assertTrue(isPlayerOsdRevealKey(Key.DirectionCenter))
        assertTrue(isPlayerOsdRevealKey(Key.Enter))
        assertFalse(isPlayerOsdRevealKey(Key.A))
    }
}
