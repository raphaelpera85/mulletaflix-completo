package org.mulletaflix.feature.home

/** Refreshes the Home feed once when connectivity returns after an offline state. */
internal fun shouldRefreshHomeOnNetworkReturn(
    previousOnline: Boolean?,
    currentOnline: Boolean,
): Boolean = previousOnline == false && currentOnline
