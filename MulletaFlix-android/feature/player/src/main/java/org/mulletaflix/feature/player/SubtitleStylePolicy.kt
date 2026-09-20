package org.mulletaflix.feature.player

import android.graphics.Color

internal const val SUBTITLE_COLOR_WHITE = "WHITE"
internal const val SUBTITLE_COLOR_YELLOW = "YELLOW"
internal const val SUBTITLE_COLOR_CYAN = "CYAN"

internal fun normalizeSubtitleColor(value: String?): String = when (value?.trim()?.uppercase()) {
    SUBTITLE_COLOR_YELLOW -> SUBTITLE_COLOR_YELLOW
    SUBTITLE_COLOR_CYAN -> SUBTITLE_COLOR_CYAN
    else -> SUBTITLE_COLOR_WHITE
}

internal fun subtitleForegroundColor(value: String?): Int = when (normalizeSubtitleColor(value)) {
    SUBTITLE_COLOR_YELLOW -> Color.YELLOW
    SUBTITLE_COLOR_CYAN -> Color.CYAN
    else -> Color.WHITE
}
