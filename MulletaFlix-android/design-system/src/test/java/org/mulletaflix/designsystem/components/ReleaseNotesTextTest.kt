package org.mulletaflix.designsystem.components

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ReleaseNotesTextTest {
    @Test
    fun `parses headings bullets and paragraphs`() {
        val lines = parseReleaseNotes("### Destaques\n- **Player** melhorado\nTexto final")

        assertEquals(
            listOf(
                ReleaseNoteLine(ReleaseNoteLineKind.Heading, "Destaques"),
                ReleaseNoteLine(ReleaseNoteLineKind.Bullet, "**Player** melhorado"),
                ReleaseNoteLine(ReleaseNoteLineKind.Paragraph, "Texto final"),
            ),
            lines,
        )
    }

    @Test
    fun `renders bold markdown as a span`() {
        val annotated = releaseNoteInlineText("**Player** melhorado")

        assertEquals("Player melhorado", annotated.text)
        assertTrue(annotated.spanStyles.any { it.item.fontWeight?.weight == 700 })
    }
}
