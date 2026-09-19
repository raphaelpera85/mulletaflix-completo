package org.mulletaflix.feature.player

import android.content.pm.ActivityInfo

/**
 * Streaming content is normally widescreen, so the player enters a landscape
 * sensor mode while preserving an already-landscape activity orientation.
 */
internal fun playerOrientationForEntry(currentOrientation: Int): Int =
    when (currentOrientation) {
        ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE,
        ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE -> currentOrientation
        else -> ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE
    }
