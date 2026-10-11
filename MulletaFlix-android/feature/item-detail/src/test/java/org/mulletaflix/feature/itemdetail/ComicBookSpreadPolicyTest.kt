package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test

class ComicBookSpreadPolicyTest {
    @Test
    fun `spread is enabled only for wide non television layouts`() {
        assertEquals(false, shouldShowComicBookSpread(900, 411, false, isComicBook = true))
        assertEquals(false, shouldShowComicBookSpread(839, 600, false, isComicBook = true))
        assertEquals(true, shouldShowComicBookSpread(840, 600, false, isComicBook = true))
        assertEquals(false, shouldShowComicBookSpread(1200, 800, true, isComicBook = true))
        assertEquals(false, shouldShowComicBookSpread(1200, 800, false, isComicBook = false))
    }

    @Test
    fun `spread pages never skip final page or exceed archive`() {
        assertEquals(listOf(0, 1), comicBookVisiblePages(currentPage = 0, pageCount = 5, useSpread = true))
        assertEquals(listOf(4), comicBookVisiblePages(currentPage = 4, pageCount = 5, useSpread = true))
        assertEquals(listOf(3), comicBookVisiblePages(currentPage = 3, pageCount = 4, useSpread = false))
    }
}
