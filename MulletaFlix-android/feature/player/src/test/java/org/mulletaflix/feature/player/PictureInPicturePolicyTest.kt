package org.mulletaflix.feature.player

import android.os.Build
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PictureInPicturePolicyTest {
    @Test
    fun `enters PiP when enabled playing and platform supports it`() {
        assertTrue(shouldEnterPictureInPicture(true, true, Build.VERSION_CODES.O))
    }

    @Test
    fun `does not enter PiP when preference is disabled`() {
        assertFalse(shouldEnterPictureInPicture(false, true, Build.VERSION_CODES.UPSIDE_DOWN_CAKE))
    }

    @Test
    fun `does not enter PiP while paused`() {
        assertFalse(shouldEnterPictureInPicture(true, false, Build.VERSION_CODES.UPSIDE_DOWN_CAKE))
    }

    @Test
    fun `does not enter PiP on unsupported platform`() {
        assertFalse(shouldEnterPictureInPicture(true, true, Build.VERSION_CODES.N_MR1))
    }

    @Test
    fun `uses automatic entry on Android 12 and newer only while playing`() {
        assertTrue(shouldUseAutomaticPictureInPicture(true, true, Build.VERSION_CODES.S))
        assertFalse(shouldUseAutomaticPictureInPicture(true, false, Build.VERSION_CODES.S))
        assertFalse(shouldUseAutomaticPictureInPicture(false, true, Build.VERSION_CODES.S))
        assertFalse(shouldUseAutomaticPictureInPicture(true, true, Build.VERSION_CODES.R))
    }

    @Test
    fun `uses user leave callback only before Android 12`() {
        assertTrue(shouldEnterPictureInPictureOnUserLeaveHint(true, true, Build.VERSION_CODES.O))
        assertTrue(shouldEnterPictureInPictureOnUserLeaveHint(true, true, Build.VERSION_CODES.R))
        assertFalse(shouldEnterPictureInPictureOnUserLeaveHint(true, true, Build.VERSION_CODES.S))
        assertFalse(shouldEnterPictureInPictureOnUserLeaveHint(true, false, Build.VERSION_CODES.R))
    }

    @Test
    fun `hides player overlays while in PiP`() {
        assertFalse(shouldShowPlayerOverlay(true))
    }

    @Test
    fun `restores player overlays after leaving PiP`() {
        assertTrue(shouldShowPlayerOverlay(false))
    }

    @Test
    fun `controller publishes PiP entry and exit to the player`() {
        PlayerPictureInPictureController.onPictureInPictureModeChanged(true)
        assertEquals(true, PlayerPictureInPictureController.isInPictureInPictureMode.value)

        PlayerPictureInPictureController.onPictureInPictureModeChanged(false)
        assertEquals(false, PlayerPictureInPictureController.isInPictureInPictureMode.value)
    }
}
