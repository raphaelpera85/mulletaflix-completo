package org.mulletaflix.designsystem.components

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

internal enum class ReleaseNoteLineKind { Heading, Bullet, Paragraph }

internal data class ReleaseNoteLine(
    val kind: ReleaseNoteLineKind,
    val text: String,
)

/** Parses the small Markdown subset used by GitHub release notes. */
internal fun parseReleaseNotes(markdown: String): List<ReleaseNoteLine> = markdown
    .lineSequence()
    .mapNotNull { rawLine ->
        val line = rawLine.trim()
        if (line.isBlank()) return@mapNotNull null
        when {
            line.startsWith("### ") -> ReleaseNoteLine(ReleaseNoteLineKind.Heading, line.removePrefix("### ").trim())
            line.startsWith("## ") -> ReleaseNoteLine(ReleaseNoteLineKind.Heading, line.removePrefix("## ").trim())
            line.startsWith("# ") -> ReleaseNoteLine(ReleaseNoteLineKind.Heading, line.removePrefix("# ").trim())
            line.startsWith("- ") || line.startsWith("* ") -> ReleaseNoteLine(ReleaseNoteLineKind.Bullet, line.drop(2).trim())
            else -> ReleaseNoteLine(ReleaseNoteLineKind.Paragraph, line)
        }
    }
    .toList()

internal fun releaseNoteInlineText(text: String): AnnotatedString = buildAnnotatedString {
    val token = Regex("(\\*\\*|__)(.+?)(\\*\\*|__)")
    var cursor = 0
    token.findAll(text).forEach { match ->
        append(text.substring(cursor, match.range.first))
        pushStyle(SpanStyle(fontWeight = FontWeight.Bold))
        append(match.groupValues[2])
        pop()
        cursor = match.range.last + 1
    }
    append(text.substring(cursor))
}

@Composable
fun ReleaseNotesText(
    markdown: String,
    modifier: Modifier = Modifier,
) {
    val lines = parseReleaseNotes(markdown)
    Column(
        modifier = modifier
            .heightIn(max = 220.dp)
            .verticalScroll(rememberScrollState()),
    ) {
        lines.forEach { line ->
            when (line.kind) {
                ReleaseNoteLineKind.Heading -> Text(
                    text = releaseNoteInlineText(line.text),
                    style = MaterialTheme.typography.titleSmall,
                    modifier = Modifier.padding(top = 4.dp, bottom = 2.dp),
                )
                ReleaseNoteLineKind.Bullet -> Row {
                    Text("• ", style = MaterialTheme.typography.bodySmall)
                    Text(
                        text = releaseNoteInlineText(line.text),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                ReleaseNoteLineKind.Paragraph -> Text(
                    text = releaseNoteInlineText(line.text),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(2.dp))
        }
    }
}
