package org.mulletaflix.feature.player

import android.os.Build
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

/** Returns whether leaving the activity should keep the current playback in PiP. */
internal fun shouldEnterPictureInPicture(
    enabled: Boolean,
    isPlaying: Boolean,
    sdkInt: Int,
): Boolean = enabled && isPlaying && sdkInt >= Build.VERSION_CODES.O

internal fun shouldShowPlayerOverlay(isInPictureInPictureMode: Boolean): Boolean =
    !isInPictureInPictureMode

internal fun shouldUseAutomaticPictureInPicture(
    enabled: Boolean,
    isPlaying: Boolean,
    sdkInt: Int,
): Boolean = enabled && isPlaying && sdkInt >= Build.VERSION_CODES.S

internal fun shouldEnterPictureInPictureOnUserLeaveHint(
    enabled: Boolean,
    isPlaying: Boolean,
    sdkInt: Int,
): Boolean = enabled && isPlaying && sdkInt >= Build.VERSION_CODES.O && sdkInt < Build.VERSION_CODES.S

/** Small bridge used by the host Activity for the system Home/user-leave event. */
object PlayerPictureInPictureController {
    private val mutableIsInPictureInPictureMode = MutableStateFlow(false)
    val isInPictureInPictureMode = mutableIsInPictureInPictureMode.asStateFlow()

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

    fun onPictureInPictureModeChanged(isInPictureInPictureMode: Boolean) {
        mutableIsInPictureInPictureMode.value = isInPictureInPictureMode
    }
}
