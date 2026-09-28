package org.mulletaflix.feature.library

import kotlin.math.roundToInt

/** Persisted choices for the adaptive library grid. */
const val LIBRARY_GRID_DENSITY_COMFORTABLE = "COMFORTABLE"
const val LIBRARY_GRID_DENSITY_COMPACT = "COMPACT"

// Grid item width is the viewport minus 16dp horizontal padding and 6dp per gap.
// At the common 480dp TV viewport, these keep five comfortable or six compact
// posters per row while accounting for the space LazyVerticalGrid consumes.
internal const val LIBRARY_GRID_HORIZONTAL_PADDING_DP = 8
internal const val LIBRARY_GRID_HORIZONTAL_SPACING_DP = 6
private const val TV_LIBRARY_COMFORTABLE_MIN_SIZE_DP = 96
private const val TV_LIBRARY_COMPACT_MIN_SIZE_DP = 80
private const val TV_LIBRARY_GRID_COMFORTABLE_CARD_MIN_WIDTH_DP = 88
private const val TV_LIBRARY_GRID_COMPACT_CARD_MIN_WIDTH_DP = 72
private const val TV_LIBRARY_NARROW_COMFORTABLE_MIN_SIZE_DP = 68
private const val TV_LIBRARY_NARROW_COMPACT_MIN_SIZE_DP = 60
private const val TV_LIBRARY_NARROW_VIEWPORT_DP = 320
private const val TV_LIBRARY_STANDARD_VIEWPORT_DP = 480
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

/** Effective poster width after accounting for grid content padding and column spacing. */
internal fun libraryGridCardWidthDp(widthDp: Int, columns: Int): Float {
    if (columns <= 0) return 0f
    val availableWidth = (
        widthDp -
            (LIBRARY_GRID_HORIZONTAL_PADDING_DP * 2) -
            (LIBRARY_GRID_HORIZONTAL_SPACING_DP * (columns - 1))
        ).coerceAtLeast(0)
    return availableWidth.toFloat() / columns
}

/** Minimum TV card width eases down on narrow logical viewports to preserve a denser grid. */
internal fun libraryGridMinimumCardSizeDp(widthDp: Int, density: String?): Int {
    val normalized = normalizeLibraryGridDensity(density)
    val standardMinimum = when (normalized) {
        LIBRARY_GRID_DENSITY_COMPACT -> TV_LIBRARY_GRID_COMPACT_CARD_MIN_WIDTH_DP
        else -> TV_LIBRARY_GRID_COMFORTABLE_CARD_MIN_WIDTH_DP
    }
    if (widthDp >= TV_LIBRARY_STANDARD_VIEWPORT_DP) return standardMinimum

    val narrowMinimum = when (normalized) {
        LIBRARY_GRID_DENSITY_COMPACT -> TV_LIBRARY_NARROW_COMPACT_MIN_SIZE_DP
        else -> TV_LIBRARY_NARROW_COMFORTABLE_MIN_SIZE_DP
    }
    val progress = (widthDp - TV_LIBRARY_NARROW_VIEWPORT_DP)
        .coerceIn(0, TV_LIBRARY_STANDARD_VIEWPORT_DP - TV_LIBRARY_NARROW_VIEWPORT_DP)
        .toFloat() / (TV_LIBRARY_STANDARD_VIEWPORT_DP - TV_LIBRARY_NARROW_VIEWPORT_DP)
    return (narrowMinimum + (standardMinimum - narrowMinimum) * progress).roundToInt()
}

/** TV uses a denser poster grid while ensuring each card fits the available grid width. */
internal fun libraryGridColumns(widthDp: Int, density: String?, isTelevision: Boolean): Int {
    if (!isTelevision) return 0
    val minimumCardWidth = libraryGridMinimumCardSizeDp(widthDp, density)
    val usableWidth = widthDp - LIBRARY_GRID_HORIZONTAL_PADDING_DP * 2
    return ((usableWidth + LIBRARY_GRID_HORIZONTAL_SPACING_DP) /
        (minimumCardWidth + LIBRARY_GRID_HORIZONTAL_SPACING_DP)).coerceAtLeast(1)
}
