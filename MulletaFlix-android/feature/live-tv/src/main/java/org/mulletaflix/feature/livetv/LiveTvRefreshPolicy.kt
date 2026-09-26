package org.mulletaflix.feature.livetv

/** Long-lived TV surfaces need fresh channels without requiring a remote action. */
internal const val TV_LIVE_REFRESH_INTERVAL_MILLIS = 60_000L
internal const val LIVE_TV_GUIDE_REFRESH_INTERVAL_MILLIS = 60_000L

internal fun liveTvAutoRefreshIntervalMillis(isTelevision: Boolean): Long =
    if (isTelevision) TV_LIVE_REFRESH_INTERVAL_MILLIS else 0L

internal fun refreshLiveTvImmediatelyOnResume(isTelevision: Boolean): Boolean = isTelevision

/** The open EPG must follow the clock without polling while it is hidden. */
internal fun shouldRefreshLiveTvGuide(isGuideOpen: Boolean): Boolean = isGuideOpen

/** An open EPG is stale after backgrounding; handhelds refresh it directly on resume. */
internal fun refreshLiveTvGuideImmediatelyOnResume(
    isGuideOpen: Boolean,
    isTelevision: Boolean,
): Boolean = isGuideOpen && !isTelevision

/** Foreground timers must not cancel a channel request already in progress. */
internal fun shouldRefreshLiveTvIfIdle(isOffline: Boolean, isLoading: Boolean): Boolean =
    !isOffline && !isLoading
