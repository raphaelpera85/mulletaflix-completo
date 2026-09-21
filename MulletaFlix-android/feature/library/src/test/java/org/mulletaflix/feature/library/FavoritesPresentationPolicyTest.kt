package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test

class FavoritesPresentationPolicyTest {
    @Test
    fun `tv uses a denser adaptive favorites grid`() {
        assertEquals(7, favoritesGridColumns(1008, isTelevision = true))
        assertEquals(4, favoritesGridColumns(600, isTelevision = false))
    }

    @Test
    fun `phone keeps three favorites columns and no background refresh`() {
        assertEquals(3, favoritesGridColumns(411, isTelevision = false))
        assertEquals(0L, favoritesAutoRefreshIntervalMillis(isTelevision = false))
    }
}
