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
}
