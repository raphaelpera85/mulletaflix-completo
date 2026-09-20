package org.mulletaflix.designsystem.components

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.material3.Text
import org.mulletaflix.designsystem.theme.MulletaFlixRed

internal const val MULLETAFLIX_WORDMARK = "MULLETAFLIX"

/** Builds the official wordmark with the brand's red/white split. */
internal fun buildMulletaFlixWordmark(
    red: Color = MulletaFlixRed,
    white: Color = Color.White,
): AnnotatedString = AnnotatedString.Builder().apply {
    pushStyle(SpanStyle(color = red))
    append("MULLETA")
    pop()
    pushStyle(SpanStyle(color = white))
    append("FLIX")
    pop()
}.toAnnotatedString()

@Composable
fun MulletaFlixWordmark(
    modifier: Modifier = Modifier,
    style: TextStyle = MaterialTheme.typography.titleLarge,
    fontWeight: FontWeight = FontWeight.Black,
    contentDescription: String = MULLETAFLIX_WORDMARK,
) {
    Text(
        text = buildMulletaFlixWordmark(),
        style = style,
        fontWeight = fontWeight,
        modifier = modifier.semantics { this.contentDescription = contentDescription },
    )
}
