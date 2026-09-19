package org.mulletaflix.android.navigation

/**
 * Decides whether a media deep link should replace the currently visible
 * authenticated destination. Authentication routes intentionally return false
 * so the existing login callback can preserve the destination after login.
 */
internal fun shouldNavigateToMediaDeepLink(
    currentRoute: String?,
    currentItemId: String?,
    targetItemId: String?,
): Boolean {
    if (targetItemId.isNullOrBlank()) return false
    if (currentRoute.isNullOrBlank()) return false
    if (!currentRoute.startsWith("main/") &&
        !currentRoute.startsWith("detail/") &&
        !currentRoute.startsWith("player/")) {
        return false
    }
    return currentItemId != targetItemId
}
