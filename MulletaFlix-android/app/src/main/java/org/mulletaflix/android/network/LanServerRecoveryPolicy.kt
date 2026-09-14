package org.mulletaflix.android.network

/** A LAN endpoint is preferred only when it is a real, different endpoint. */
internal fun shouldSwitchToLan(currentUrl: String, discoveredUrl: String): Boolean =
    discoveredUrl.isNotBlank() &&
    !discoveredUrl.equals(currentUrl.trimEnd('/'), ignoreCase = true)

/** A successful login is enough to trigger a LAN re-check. */
internal fun shouldScanAfterAuthentication(userId: String?): Boolean =
    !userId.isNullOrBlank()
