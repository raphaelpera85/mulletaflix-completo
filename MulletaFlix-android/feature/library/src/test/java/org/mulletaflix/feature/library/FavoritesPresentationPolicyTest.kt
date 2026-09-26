package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test

class FavoritesPresentationPolicyTest {
    @Test
    fun `tv uses a denser adaptive favorites grid`() {
        assertEquals(5, favoritesGridColumns(480, isTelevision = true))
        assertEquals(10, favoritesGridColumns(1008, isTelevision = true))
        assertEquals(4, favoritesGridColumns(600, isTelevision = false))
    }

    @Test
    fun `compact preference is shared by phone tablet and tv favorites`() {
        assertEquals(4, favoritesGridColumns(411, isTelevision = false, density = LIBRARY_GRID_DENSITY_COMPACT))
        assertEquals(5, favoritesGridColumns(600, isTelevision = false, density = LIBRARY_GRID_DENSITY_COMPACT))
        assertEquals(6, favoritesGridColumns(480, isTelevision = true, density = LIBRARY_GRID_DENSITY_COMPACT))
    }

    @Test
    fun `phone keeps three favorites columns and no background refresh`() {
        assertEquals(3, favoritesGridColumns(411, isTelevision = false))
        assertEquals(0L, favoritesAutoRefreshIntervalMillis(isTelevision = false))
    }
}
