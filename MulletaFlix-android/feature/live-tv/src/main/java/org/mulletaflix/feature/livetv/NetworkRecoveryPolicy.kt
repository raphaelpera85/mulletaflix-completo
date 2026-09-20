package org.mulletaflix.feature.livetv

/** Refreshes the live catalog once after an offline session becomes online. */
internal fun shouldRefreshLiveTvOnNetworkReturn(
    previousOnline: Boolean?,
    currentOnline: Boolean,
): Boolean = previousOnline == false && currentOnline
