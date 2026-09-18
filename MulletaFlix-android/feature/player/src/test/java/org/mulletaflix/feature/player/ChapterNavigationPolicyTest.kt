package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.domain.model.Chapter

class ChapterNavigationPolicyTest {

    private val sampleChapters = listOf(
        Chapter(startPositionTicks = 0L, name = "Abertura"),
        Chapter(startPositionTicks = 60_000_0000L, name = "Capítulo 1"), // 60s
        Chapter(startPositionTicks = 180_000_0000L, name = "Capítulo 2"), // 180s
        Chapter(startPositionTicks = 300_000_0000L, name = "Encerramento"), // 300s
    )

    @Test
    fun findCurrentChapter_returnsCorrectChapter() {
        val chapterAtZero = findCurrentChapter(sampleChapters, 0L)
        assertEquals("Abertura", chapterAtZero?.name)

        val chapterAt90s = findCurrentChapter(sampleChapters, 90_000L)
        assertEquals("Capítulo 1", chapterAt90s?.name)

        val chapterAt250s = findCurrentChapter(sampleChapters, 250_000L)
        assertEquals("Capítulo 2", chapterAt250s?.name)
    }

    @Test
    fun findCurrentChapter_emptyListReturnsNull() {
        assertNull(findCurrentChapter(emptyList(), 5000L))
    }

    @Test
    fun findNextChapterPosition_returnsTargetPosition() {
        // At 10s, next chapter is at 60s
        val nextFrom10s = findNextChapterPosition(sampleChapters, 10_000L)
        assertEquals(60_000L, nextFrom10s)

        // At 70s, next chapter is at 180s
        val nextFrom70s = findNextChapterPosition(sampleChapters, 70_000L)
        assertEquals(180_000L, nextFrom70s)

        // Beyond last chapter, returns null
        val nextFrom350s = findNextChapterPosition(sampleChapters, 350_000L)
        assertNull(nextFrom350s)
    }

    @Test
    fun findPreviousChapterPosition_restartsCurrentIfMoreThan3sIn() {
        // At 70s (10s into Chapter 1 which starts at 60s), should restart Chapter 1 at 60s
        val prevFrom70s = findPreviousChapterPosition(sampleChapters, 70_000L)
        assertEquals(60_000L, prevFrom70s)
    }

    @Test
    fun findPreviousChapterPosition_goesToPreviousIfWithin3s() {
        // At 61s (1s into Chapter 1), should jump back to previous chapter (Abertura at 0s)
        val prevFrom61s = findPreviousChapterPosition(sampleChapters, 61_000L)
        assertEquals(0L, prevFrom61s)
    }
}
