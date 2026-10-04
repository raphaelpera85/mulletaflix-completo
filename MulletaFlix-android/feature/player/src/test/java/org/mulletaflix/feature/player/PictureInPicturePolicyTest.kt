package org.mulletaflix.feature.player

import android.os.Build
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PictureInPicturePolicyTest {
    @Test
    fun `enters PiP when enabled playing and platform supports it`() {
        assertTrue(shouldEnterPictureInPicture(true, true, Build.VERSION_CODES.O, isTelevision = false))
        assertFalse(shouldEnterPictureInPicture(true, true, Build.VERSION_CODES.O, isTelevision = true))
    }

    @Test
    fun `does not enter PiP when preference is disabled`() {
        assertFalse(shouldEnterPictureInPicture(false, true, Build.VERSION_CODES.UPSIDE_DOWN_CAKE, isTelevision = false))
    }

    @Test
    fun `does not enter PiP while paused`() {
        assertFalse(shouldEnterPictureInPicture(true, false, Build.VERSION_CODES.UPSIDE_DOWN_CAKE, isTelevision = false))
    }

    @Test
    fun `does not enter PiP on unsupported platform`() {
        assertFalse(shouldEnterPictureInPicture(true, true, Build.VERSION_CODES.N_MR1, isTelevision = false))
    }

    @Test
    fun `uses automatic entry on Android 12 and newer only while playing`() {
        assertTrue(shouldUseAutomaticPictureInPicture(true, true, Build.VERSION_CODES.S, isTelevision = false))
        assertFalse(shouldUseAutomaticPictureInPicture(true, true, Build.VERSION_CODES.S, isTelevision = true))
        assertFalse(shouldUseAutomaticPictureInPicture(true, false, Build.VERSION_CODES.S, isTelevision = false))
        assertFalse(shouldUseAutomaticPictureInPicture(false, true, Build.VERSION_CODES.S, isTelevision = false))
        assertFalse(shouldUseAutomaticPictureInPicture(true, true, Build.VERSION_CODES.R, isTelevision = false))
    }

    @Test
    fun `uses user leave callback only before Android 12`() {
        assertTrue(shouldEnterPictureInPictureOnUserLeaveHint(true, true, Build.VERSION_CODES.O, isTelevision = false))
        assertTrue(shouldEnterPictureInPictureOnUserLeaveHint(true, true, Build.VERSION_CODES.R, isTelevision = false))
        assertFalse(shouldEnterPictureInPictureOnUserLeaveHint(true, true, Build.VERSION_CODES.O, isTelevision = true))
        assertFalse(shouldEnterPictureInPictureOnUserLeaveHint(true, true, Build.VERSION_CODES.S, isTelevision = false))
        assertFalse(shouldEnterPictureInPictureOnUserLeaveHint(true, false, Build.VERSION_CODES.R, isTelevision = false))
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
