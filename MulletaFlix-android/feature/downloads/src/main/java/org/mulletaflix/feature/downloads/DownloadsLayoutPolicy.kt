package org.mulletaflix.feature.downloads

internal fun downloadsContentMaxWidthDp(
    availableWidthDp: Int,
    isTelevision: Boolean,
): Int = when {
    isTelevision -> 1200
    availableWidthDp >= 600 -> 960
    else -> availableWidthDp
}
