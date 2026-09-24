package org.mulletaflix.feature.library

/** Library pages are long-lived on TV and must reconcile server changes while visible. */
internal const val TV_LIBRARY_AUTO_REFRESH_INTERVAL_MILLIS = 60_000L

internal fun libraryAutoRefreshIntervalMillis(isTelevision: Boolean): Long =
    if (isTelevision) TV_LIBRARY_AUTO_REFRESH_INTERVAL_MILLIS else 0L

internal fun libraryRefreshImmediatelyOnResume(isTelevision: Boolean): Boolean = isTelevision

/** Refreshes a library once when connectivity returns after an offline state. */
internal fun shouldRefreshLibraryOnNetworkReturn(
    previousOnline: Boolean?,
    currentOnline: Boolean,
): Boolean = previousOnline == false && currentOnline

internal const val TV_FAVORITES_REFRESH_INTERVAL_MILLIS = 60_000L

internal fun favoritesAutoRefreshIntervalMillis(isTelevision: Boolean): Long =
    if (isTelevision) TV_FAVORITES_REFRESH_INTERVAL_MILLIS else 0L

/** Keeps poster cards readable while honoring the shared grid-density preference. */
internal fun favoritesGridColumns(
    widthDp: Int,
    isTelevision: Boolean,
    density: String = LIBRARY_GRID_DENSITY_COMFORTABLE,
): Int {
    val normalized = normalizeLibraryGridDensity(density)
    return when {
        isTelevision -> (widthDp / libraryGridMinSizeDp(normalized, isTelevision = true)).coerceAtLeast(5)
        widthDp >= 600 -> (widthDp / libraryGridMinSizeDp(normalized, isTablet = true)).coerceAtLeast(4)
        else -> (widthDp / libraryGridMinSizeDp(normalized)).coerceAtLeast(3)
    }
}
