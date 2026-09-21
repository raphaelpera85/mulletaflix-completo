package org.mulletaflix.feature.library

/** Library pages are long-lived on TV and must reconcile server changes while visible. */
internal const val LIBRARY_AUTO_REFRESH_INTERVAL_MILLIS = 60_000L

internal fun libraryRefreshImmediatelyOnResume(): Boolean = true

internal const val TV_FAVORITES_REFRESH_INTERVAL_MILLIS = 60_000L

internal fun favoritesAutoRefreshIntervalMillis(isTelevision: Boolean): Long =
    if (isTelevision) TV_FAVORITES_REFRESH_INTERVAL_MILLIS else 0L

/** Keeps poster cards readable while making better use of tablet/TV width. */
internal fun favoritesGridColumns(widthDp: Int, isTelevision: Boolean): Int = when {
    isTelevision -> (widthDp / 132).coerceAtLeast(4)
    widthDp >= 600 -> (widthDp / 150).coerceAtLeast(4)
    else -> 3
}
