package org.mulletaflix.feature.search

internal fun searchContentMaxWidthDp(availableWidthDp: Int, isTelevision: Boolean): Int =
    when {
        isTelevision -> 1280
        availableWidthDp >= 600 -> 1000
        else -> availableWidthDp
    }
