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
        assertEquals(84, libraryGridMinSizeDp(LIBRARY_GRID_DENSITY_COMFORTABLE, isTelevision = true))
        assertEquals(72, libraryGridMinSizeDp(LIBRARY_GRID_DENSITY_COMPACT, isTelevision = true))
        assertEquals(12, libraryGridColumns(1008, LIBRARY_GRID_DENSITY_COMFORTABLE, isTelevision = true))
    }

    @Test
    fun `phone keeps adaptive columns instead of television fixed policy`() {
        assertEquals(0, libraryGridColumns(411, LIBRARY_GRID_DENSITY_COMFORTABLE, isTelevision = false))
    }
}
