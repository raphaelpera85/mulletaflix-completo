package org.mulletaflix.feature.player

import android.content.pm.ActivityInfo
import org.junit.Assert.assertEquals
import org.junit.Test

class PlayerOrientationPolicyTest {
    @Test
    fun `portrait entry requests sensor landscape`() {
        assertEquals(
            ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE,
            playerOrientationForEntry(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT),
        )
    }

    @Test
    fun `unspecified entry requests sensor landscape`() {
        assertEquals(
            ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE,
            playerOrientationForEntry(ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED),
        )
    }

    @Test
    fun `landscape entry keeps the explicit orientation`() {
        assertEquals(
            ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE,
            playerOrientationForEntry(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE),
        )
        assertEquals(
            ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE,
            playerOrientationForEntry(ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE),
        )
    }
}
