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
internal fun libraryGridMinSizeDp(value: String?, isTelevision: Boolean = false): Int =
    when (normalizeLibraryGridDensity(value)) {
        LIBRARY_GRID_DENSITY_COMPACT -> if (isTelevision) 112 else 92
        else -> if (isTelevision) 132 else 112
    }

/** TV uses a denser poster grid so remote navigation can scan more titles at once. */
internal fun libraryGridColumns(widthDp: Int, density: String?, isTelevision: Boolean): Int {
    if (!isTelevision) return 0
    return (widthDp / libraryGridMinSizeDp(density, isTelevision = true)).coerceAtLeast(1)
}
