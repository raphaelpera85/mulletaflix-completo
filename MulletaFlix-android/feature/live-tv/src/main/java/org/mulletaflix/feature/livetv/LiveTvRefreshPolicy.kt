package org.mulletaflix.feature.livetv

/** Long-lived TV surfaces need fresh channels without requiring a remote action. */
internal const val TV_LIVE_REFRESH_INTERVAL_MILLIS = 60_000L

internal fun liveTvAutoRefreshIntervalMillis(isTelevision: Boolean): Long =
    if (isTelevision) TV_LIVE_REFRESH_INTERVAL_MILLIS else 0L

internal fun refreshLiveTvImmediatelyOnResume(isTelevision: Boolean): Boolean = isTelevision

/** Foreground timers must not cancel a channel request already in progress. */
internal fun shouldRefreshLiveTvIfIdle(isOffline: Boolean, isLoading: Boolean): Boolean =
    !isOffline && !isLoading
