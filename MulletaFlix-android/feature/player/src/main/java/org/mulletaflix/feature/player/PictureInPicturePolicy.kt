package org.mulletaflix.feature.player

import android.os.Build

/** Returns whether leaving the activity should keep the current playback in PiP. */
internal fun shouldEnterPictureInPicture(
    enabled: Boolean,
    isPlaying: Boolean,
    sdkInt: Int,
): Boolean = enabled && isPlaying && sdkInt >= Build.VERSION_CODES.O

/** Small bridge used by the host Activity for the system Home/user-leave event. */
object PlayerPictureInPictureController {
    private var onUserLeave: (() -> Unit)? = null

    fun register(callback: () -> Unit) {
        onUserLeave = callback
    }

    fun unregister() {
        onUserLeave = null
    }

    fun dispatchUserLeaveHint() {
        onUserLeave?.invoke()
    }
}
