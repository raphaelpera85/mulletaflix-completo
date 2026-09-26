package org.mulletaflix.feature.library

/** Persisted choices for the adaptive library grid. */
const val LIBRARY_GRID_DENSITY_COMFORTABLE = "COMFORTABLE"
const val LIBRARY_GRID_DENSITY_COMPACT = "COMPACT"

// Android TV commonly reports a 480dp logical viewport on a 1080p panel.
// 132dp therefore produced only three oversized posters; these values keep
// five comfortable or six compact titles visible per row on that surface.
private const val TV_LIBRARY_COMFORTABLE_MIN_SIZE_DP = 96
private const val TV_LIBRARY_COMPACT_MIN_SIZE_DP = 80
private const val TABLET_LIBRARY_COMFORTABLE_MIN_SIZE_DP = 140
private const val TABLET_LIBRARY_COMPACT_MIN_SIZE_DP = 116

internal fun normalizeLibraryGridDensity(value: String?): String =
    when (value?.trim()?.uppercase()) {
        LIBRARY_GRID_DENSITY_COMPACT -> LIBRARY_GRID_DENSITY_COMPACT
        else -> LIBRARY_GRID_DENSITY_COMFORTABLE
    }

/** Minimum width in dp; Adaptive then chooses the best column count for the viewport. */
internal fun libraryGridMinSizeDp(
    value: String?,
    isTelevision: Boolean = false,
    isTablet: Boolean = false,
): Int =
    when (normalizeLibraryGridDensity(value)) {
        LIBRARY_GRID_DENSITY_COMPACT -> when {
            isTelevision -> TV_LIBRARY_COMPACT_MIN_SIZE_DP
            isTablet -> TABLET_LIBRARY_COMPACT_MIN_SIZE_DP
            else -> 92
        }
        else -> when {
            isTelevision -> TV_LIBRARY_COMFORTABLE_MIN_SIZE_DP
            isTablet -> TABLET_LIBRARY_COMFORTABLE_MIN_SIZE_DP
            else -> 112
        }
    }

/** TV uses a denser poster grid so remote navigation can scan more titles at once. */
internal fun libraryGridColumns(widthDp: Int, density: String?, isTelevision: Boolean): Int {
    if (!isTelevision) return 0
    return (widthDp / libraryGridMinSizeDp(density, isTelevision = true)).coerceAtLeast(1)
}
