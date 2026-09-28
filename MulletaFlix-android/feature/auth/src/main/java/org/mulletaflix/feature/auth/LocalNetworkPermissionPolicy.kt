package org.mulletaflix.feature.auth

internal const val ANDROID_17_API = 37

internal fun requiresLocalNetworkPermission(
    sdkInt: Int,
    targetSdk: Int,
    permissionGranted: Boolean,
): Boolean = sdkInt >= ANDROID_17_API &&
    targetSdk >= ANDROID_17_API &&
    !permissionGranted
