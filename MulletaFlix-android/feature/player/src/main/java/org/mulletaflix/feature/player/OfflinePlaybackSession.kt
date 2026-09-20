package org.mulletaflix.feature.player

/** Uses persisted auth as the authority and the observed session only as a startup fallback. */
internal fun resolveOfflinePlaybackUserId(cachedUserId: String?, persistedUserId: String?): String? =
    persistedUserId?.takeIf { it.isNotBlank() } ?: cachedUserId?.takeIf { it.isNotBlank() }
