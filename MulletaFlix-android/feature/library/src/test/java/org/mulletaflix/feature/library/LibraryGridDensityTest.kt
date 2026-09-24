package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test

class LibraryGridDensityTest {
    @Test
    fun `invalid and blank density values use comfortable layout`() {
        assertEquals(LIBRARY_GRID_DENSITY_COMFORTABLE, normalizeLibraryGridDensity(null))
        assertEquals(LIBRARY_GRID_DENSITY_COMFORTABLE, normalizeLibraryGridDensity("unknown"))
        assertEquals(112, libraryGridMinSizeDp(""))
    }

    @Test
    fun `compact density uses smaller minimum card width`() {
        assertEquals(LIBRARY_GRID_DENSITY_COMPACT, normalizeLibraryGridDensity(" compact "))
        assertEquals(92, libraryGridMinSizeDp(LIBRARY_GRID_DENSITY_COMPACT))
    }

    @Test
    fun `television uses smaller cards and multiple predictable columns`() {
        assertEquals(96, libraryGridMinSizeDp(LIBRARY_GRID_DENSITY_COMFORTABLE, isTelevision = true))
        assertEquals(80, libraryGridMinSizeDp(LIBRARY_GRID_DENSITY_COMPACT, isTelevision = true))
        assertEquals(5, libraryGridColumns(480, LIBRARY_GRID_DENSITY_COMFORTABLE, isTelevision = true))
        assertEquals(6, libraryGridColumns(480, LIBRARY_GRID_DENSITY_COMPACT, isTelevision = true))
        assertEquals(10, libraryGridColumns(1008, LIBRARY_GRID_DENSITY_COMFORTABLE, isTelevision = true))
    }

    @Test
    fun `phone keeps adaptive columns instead of television fixed policy`() {
        assertEquals(0, libraryGridColumns(411, LIBRARY_GRID_DENSITY_COMFORTABLE, isTelevision = false))
    }

    @Test
    fun `tablet uses a readable adaptive card size distinct from phone`() {
        assertEquals(
            140,
            libraryGridMinSizeDp(
                LIBRARY_GRID_DENSITY_COMFORTABLE,
                isTablet = true,
            ),
        )
        assertEquals(
            116,
            libraryGridMinSizeDp(
                LIBRARY_GRID_DENSITY_COMPACT,
                isTablet = true,
            ),
        )
        assertEquals(112, libraryGridMinSizeDp(LIBRARY_GRID_DENSITY_COMFORTABLE))
    }
}
