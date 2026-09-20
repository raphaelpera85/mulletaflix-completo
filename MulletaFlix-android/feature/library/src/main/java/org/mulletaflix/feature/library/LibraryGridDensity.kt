package org.mulletaflix.feature.library

/** Persisted choices for the adaptive library grid. */
const val LIBRARY_GRID_DENSITY_COMFORTABLE = "COMFORTABLE"
const val LIBRARY_GRID_DENSITY_COMPACT = "COMPACT"

internal fun normalizeLibraryGridDensity(value: String?): String =
    when (value?.trim()?.uppercase()) {
        LIBRARY_GRID_DENSITY_COMPACT -> LIBRARY_GRID_DENSITY_COMPACT
        else -> LIBRARY_GRID_DENSITY_COMFORTABLE
    }

/** Minimum width in dp; Adaptive then chooses the best column count for the viewport. */
internal fun libraryGridMinSizeDp(value: String?): Int =
    when (normalizeLibraryGridDensity(value)) {
        LIBRARY_GRID_DENSITY_COMPACT -> 92
        else -> 112
    }
