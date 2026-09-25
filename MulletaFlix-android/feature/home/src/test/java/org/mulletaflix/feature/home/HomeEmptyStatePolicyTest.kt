package org.mulletaflix.feature.home

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class HomeEmptyStatePolicyTest {
    @Test
    fun `empty message appears only when all feed sections are truly empty`() {
        assertTrue(shouldShowEmptyHomeState(HomeState(isLoading = false)))
        assertFalse(
            shouldShowEmptyHomeState(
                HomeState(
                    isLoading = false,
                    favoriteItems = listOf(MediaItem("favorite", "Favorito", MediaItemType.Movie)),
                ),
            ),
        )
        assertFalse(shouldShowEmptyHomeState(HomeState(isLoading = false, resumeError = "Falha")))
    }
}
