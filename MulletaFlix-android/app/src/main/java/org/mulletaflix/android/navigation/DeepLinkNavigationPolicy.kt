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

/** Marks a link as handled once its destination is already visible. */
internal fun shouldMarkMediaDeepLinkHandled(
    currentItemId: String?,
    targetItemId: String?,
): Boolean = !targetItemId.isNullOrBlank() && currentItemId == targetItemId

/**
 * Decides whether a delivered request still needs to be acted on.
 *
 * Delivery is keyed by [requestSequence], not by the item id: re-opening the
 * same link while the app is already running must navigate again, and only the
 * *same* request may be ignored. An id-keyed check silently dropped the second
 * tap on the same link.
 */
internal fun shouldDeliverMediaDeepLink(
    requestSequence: Long?,
    handledSequence: Long?,
    itemId: String?,
): Boolean = requestSequence != null &&
    !itemId.isNullOrBlank() &&
    handledSequence != requestSequence
