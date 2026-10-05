package org.mulletaflix.feature.player

import android.text.SpannableString
import android.text.Spanned
import android.text.style.BackgroundColorSpan
import android.text.style.ForegroundColorSpan
import android.text.style.StyleSpan
import android.text.style.TextAppearanceSpan
import android.text.style.TypefaceSpan
import android.graphics.Typeface
import androidx.media3.common.util.UnstableApi
import androidx.media3.common.Player
import androidx.media3.common.text.Cue
import androidx.media3.common.text.CueGroup
import androidx.media3.ui.CaptionStyleCompat
import androidx.media3.ui.SubtitleView
import org.mulletaflix.domain.model.subtitleBackgroundArgb
import org.mulletaflix.domain.model.subtitleFractionalTextSize

/** Builds the Media3 caption style from the same stored preference used by settings preview. */
@UnstableApi
internal fun subtitleCaptionStyle(
    foregroundColor: Int,
    edgeColor: Int,
    backgroundCode: String,
): CaptionStyleCompat = CaptionStyleCompat(
    foregroundColor,
    subtitleBackgroundArgb(backgroundCode),
    android.graphics.Color.TRANSPARENT,
    CaptionStyleCompat.EDGE_TYPE_OUTLINE,
    edgeColor,
    null,
)

/** Ensures user choices win over optional formatting embedded in subtitle files. */
@UnstableApi
internal fun applyUserSubtitlePreferences(
    subtitleView: SubtitleView,
    fontSizePercent: Int,
    style: CaptionStyleCompat,
) {
    subtitleView.setApplyEmbeddedStyles(true)
    subtitleView.setApplyEmbeddedFontSizes(false)
    subtitleView.setFractionalTextSize(subtitleFractionalTextSize(fontSizePercent))
    subtitleView.setStyle(style)
}

/** Removes embedded colors and sizes while preserving typeface and emphasis. */
@UnstableApi
internal fun applyUserSubtitleColors(cue: Cue): Cue {
    val text = cue.text
    if (text == null) return cue

    val builder = cue.buildUpon()
    if (text is Spanned) {
        val editableText = SpannableString(text)
        editableText.getSpans(0, editableText.length, ForegroundColorSpan::class.java)
            .forEach(editableText::removeSpan)
        editableText.getSpans(0, editableText.length, BackgroundColorSpan::class.java)
            .forEach(editableText::removeSpan)
        editableText.getSpans(0, editableText.length, TextAppearanceSpan::class.java)
            .forEach { appearance ->
                if (appearance.textColor != null || appearance.linkTextColor != null || appearance.textSize >= 0) {
                    val start = editableText.getSpanStart(appearance)
                    val end = editableText.getSpanEnd(appearance)
                    val flags = editableText.getSpanFlags(appearance)
                    val family = appearance.family
                    val style = appearance.textStyle
                    editableText.removeSpan(appearance)
                    if (family != null) {
                        editableText.setSpan(TypefaceSpan(family), start, end, flags)
                    }
                    if (style != Typeface.NORMAL) {
                        editableText.setSpan(StyleSpan(style), start, end, flags)
                    }
                }
            }
        builder.setText(editableText)
    }
    if (cue.windowColorSet) builder.clearWindowColor()
    if (text !is Spanned && !cue.windowColorSet) return cue
    return builder.build()
}

/** Re-applies user colors after PlayerView receives Media3's original cues. */
@UnstableApi
internal fun userSubtitleCueStyleListener(subtitleView: SubtitleView): Player.Listener =
    object : Player.Listener {
        override fun onCues(cueGroup: CueGroup) {
            val userStyledCues = cueGroup.cues.map(::applyUserSubtitleColors)
            subtitleView.post { subtitleView.setCues(userStyledCues) }
        }
    }
