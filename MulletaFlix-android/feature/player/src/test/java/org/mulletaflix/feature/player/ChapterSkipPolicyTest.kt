package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.domain.model.Chapter

class ChapterSkipPolicyTest {

    @Test
    fun `returns next chapter as intro target`() {
        val chapters = listOf(
            Chapter(0, "Opening"),
            Chapter(180_000_000, "Episode"),
        )

        assertEquals(
            ChapterSkipAction(ChapterSkipKind.INTRO, 18_000),
            chapterSkipAction(chapters, 5_000),
        )
    }

    @Test
    fun `returns next chapter as credits target`() {
        val chapters = listOf(
            Chapter(0, "Episode"),
            Chapter(600_000_000, "Créditos"),
            Chapter(720_000_000, "Fim"),
        )

        assertEquals(
            ChapterSkipAction(ChapterSkipKind.CREDITS, 72_000),
            chapterSkipAction(chapters, 65_000),
        )
    }

    @Test
    fun `does not offer skip without a following chapter`() {
        assertNull(chapterSkipAction(listOf(Chapter(0, "Intro")), 1_000))
    }
}
