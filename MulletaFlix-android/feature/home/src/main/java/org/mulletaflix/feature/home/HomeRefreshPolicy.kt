package org.mulletaflix.feature.home

/**
 * TV screens stay open for long periods and need to reflect new media without
 * requiring a remote-control refresh action. Handheld layouts keep their
 * existing explicit/pull-to-refresh behavior to avoid background traffic.
 */
internal const val TV_HOME_REFRESH_INTERVAL_MILLIS = 60_000L

internal fun homeAutoRefreshIntervalMillis(isTelevision: Boolean): Long =
    if (isTelevision) TV_HOME_REFRESH_INTERVAL_MILLIS else 0L

/** A TV must reconcile stale catalog data as soon as the app becomes visible. */
internal fun refreshHomeImmediatelyOnResume(isTelevision: Boolean): Boolean = isTelevision
