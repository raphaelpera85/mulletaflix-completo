package org.mulletaflix.feature.player

/** Android 13 (API 33) introduced runtime notification permission. */
internal fun needsNotificationPermission(sdkInt: Int, permissionGranted: Boolean): Boolean =
    sdkInt >= 33 && !permissionGranted

internal fun shouldShowNotificationPermissionPrompt(
    sdkInt: Int,
    permissionGranted: Boolean,
    promptDismissed: Boolean,
): Boolean =
    !promptDismissed && needsNotificationPermission(sdkInt, permissionGranted)
