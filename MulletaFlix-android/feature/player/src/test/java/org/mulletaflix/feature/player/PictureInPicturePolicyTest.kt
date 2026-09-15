package org.mulletaflix.feature.player

import android.os.Build
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
}
