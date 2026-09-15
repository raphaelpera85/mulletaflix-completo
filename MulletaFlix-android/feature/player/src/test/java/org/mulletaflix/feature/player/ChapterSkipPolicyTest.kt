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

    @Test
    fun `mediaSegmentSkipAction offers intro skip accurately within segment bounds`() {
        val segments = listOf(
            org.mulletaflix.domain.model.MediaSegment(
                id = "seg-intro",
                itemId = "ep1",
                type = org.mulletaflix.domain.model.MediaSegmentType.Intro,
                startTicks = 600_000_000L, // 60,000 ms
                endTicks = 1_500_000_000L,  // 150,000 ms
            )
        )

        // Before intro
        assertNull(mediaSegmentSkipAction(segments, 59_999))

        // Inside intro
        assertEquals(
            ChapterSkipAction(ChapterSkipKind.INTRO, 150_000),
            mediaSegmentSkipAction(segments, 60_000)
        )
        assertEquals(
            ChapterSkipAction(ChapterSkipKind.INTRO, 150_000),
            mediaSegmentSkipAction(segments, 100_000)
        )

        // At or past end of intro
        assertNull(mediaSegmentSkipAction(segments, 150_000))
        assertNull(mediaSegmentSkipAction(segments, 150_001))
    }

    @Test
    fun `mediaSegmentSkipAction offers outro credits skip`() {
        val segments = listOf(
            org.mulletaflix.domain.model.MediaSegment(
                id = "seg-credits",
                itemId = "ep1",
                type = org.mulletaflix.domain.model.MediaSegmentType.Outro,
                startTicks = 12_000_000_000L, // 1,200,000 ms
                endTicks = 13_200_000_000L,   // 1,320,000 ms
            )
        )

        assertEquals(
            ChapterSkipAction(ChapterSkipKind.CREDITS, 1_320_000),
            mediaSegmentSkipAction(segments, 1_250_000)
        )
    }

    @Test
    fun `skipAction prioritizes media segments over chapters`() {
        val segments = listOf(
            org.mulletaflix.domain.model.MediaSegment(
                id = "seg-intro",
                itemId = "ep1",
                type = org.mulletaflix.domain.model.MediaSegmentType.Intro,
                startTicks = 600_000_000L,
                endTicks = 1_500_000_000L,
            )
        )
        val chapters = listOf(
            Chapter(0, "Opening"),
            Chapter(200_000_000, "Episode"),
        )

        // At 100s, segment matches Intro -> target 150s
        assertEquals(
            ChapterSkipAction(ChapterSkipKind.INTRO, 150_000),
            skipAction(segments, chapters, 100_000)
        )
    }
}
