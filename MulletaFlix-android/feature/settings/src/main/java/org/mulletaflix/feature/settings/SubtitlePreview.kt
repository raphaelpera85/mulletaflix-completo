package org.mulletaflix.feature.settings

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shadow
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import org.mulletaflix.designsystem.subtitle.subtitleForegroundColor
import org.mulletaflix.domain.model.subtitleFractionalTextSize

/**
 * Height of the video area the preview stands in for, in dp.
 *
 * The player sizes subtitles as a fraction of the video's height, so the preview
 * needs the same *reference* height to grow in step with it. A 360 dp plate keeps
 * 100% readable and makes 200% obviously larger.
 */
internal const val SUBTITLE_PREVIEW_REFERENCE_HEIGHT_DP = 360

/**
 * Backdrop the sample is drawn on.
 *
 * Exposed so the render test can count "everything that is not the plate"
 * instead of hardcoding the colour, which would rot the moment the plate is
 * restyled.
 */
internal val SUBTITLE_PREVIEW_PLATE_COLOR = Color(0xFF101014)

/**
 * Preview text size, in sp, for a stored subtitle percentage.
 *
 * Uses the same fractional size the player hands to Media3, so "150%" is the same
 * step up in both places instead of a second formula that could drift.
 */
internal fun subtitlePreviewFontSizeSp(
    sizePercent: Int,
    referenceHeightDp: Int = SUBTITLE_PREVIEW_REFERENCE_HEIGHT_DP,
): Float = referenceHeightDp * subtitleFractionalTextSize(sizePercent)

/**
 * Shows what the chosen subtitle size and colour look like.
 *
 * Both settings were blind: the screen showed "150%" and "Amarelo" with no way to
 * judge either without starting a video. Drawn through the same `:design-system`
 * mapping the player uses, so it cannot promise a colour or a size the player will
 * not produce. The sample sits on a dark plate because the player always outlines
 * subtitles against video.
 */
@Composable
internal fun SubtitlePreview(
    sizePercent: Int,
    colorCode: String,
    modifier: Modifier = Modifier,
) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(SUBTITLE_PREVIEW_PLATE_COLOR)
            .padding(vertical = 20.dp, horizontal = 16.dp),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = "Exemplo de legenda",
            color = subtitleForegroundColor(colorCode),
            textAlign = TextAlign.Center,
            maxLines = 2,
            style = TextStyle(
                fontSize = subtitlePreviewFontSizeSp(sizePercent).sp,
                fontWeight = FontWeight.Medium,
                shadow = Shadow(color = Color.Black, offset = Offset(0f, 2f), blurRadius = 4f),
            ),
        )
    }
}
