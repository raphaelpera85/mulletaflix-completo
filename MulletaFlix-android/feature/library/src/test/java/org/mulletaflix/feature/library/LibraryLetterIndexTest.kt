package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class LibraryLetterIndexTest {
    @Test
    fun `creates one target per present letter and ignores Portuguese accents`() {
        val targets = libraryLetterTargets(
            listOf(
                MediaItem("1", "Árvore", MediaItemType.Movie),
                MediaItem("2", "Aventura", MediaItemType.Movie),
                MediaItem("3", "Épico", MediaItemType.Movie),
                MediaItem("4", "  #Tópicos", MediaItemType.Movie),
            ),
        )

        assertEquals(listOf("A", "E", "#"), targets.map { it.letter })
        assertEquals(listOf(0, 2, 3), targets.map { it.itemIndex })
    }

    @Test
    fun `empty catalog has no letter targets`() {
        assertEquals(emptyList<LibraryLetterTarget>(), libraryLetterTargets(emptyList()))
    }

    @Test
    fun `grid jump accounts for current error and filter headers`() {
        assertEquals(7, libraryGridTargetIndex(7, hasLoadError = false, hasActiveFilters = false))
        assertEquals(8, libraryGridTargetIndex(7, hasLoadError = true, hasActiveFilters = false))
        assertEquals(8, libraryGridTargetIndex(7, hasLoadError = false, hasActiveFilters = true))
        assertEquals(9, libraryGridTargetIndex(7, hasLoadError = true, hasActiveFilters = true))
    }
}
