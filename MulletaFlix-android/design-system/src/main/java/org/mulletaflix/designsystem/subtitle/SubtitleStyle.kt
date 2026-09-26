package org.mulletaflix.designsystem.subtitle

import androidx.compose.ui.graphics.Color
import org.mulletaflix.domain.model.SUBTITLE_COLOR_CYAN
import org.mulletaflix.domain.model.SUBTITLE_COLOR_YELLOW
import org.mulletaflix.domain.model.normalizeSubtitleColor

/**
 * The Compose half of subtitle styling.
 *
 * The codes, the accepted set, the size range and the size maths live in `:domain`
 * (`SubtitleStylePolicy`), because they are what the app stores and what the server
 * receives — and because `:data` needs them too. This file is only what cannot live
 * there: turning a stored code into a `Color`.
 */

/** The colour a subtitle is drawn in. */
fun subtitleForegroundColor(value: String?): Color = when (normalizeSubtitleColor(value)) {
    SUBTITLE_COLOR_YELLOW -> Color.Yellow
    SUBTITLE_COLOR_CYAN -> Color.Cyan
    else -> Color.White
}

/** The colour the subtitle outline is drawn in; the player always uses an outline. */
val SUBTITLE_OUTLINE_COLOR: Color = Color.Black
