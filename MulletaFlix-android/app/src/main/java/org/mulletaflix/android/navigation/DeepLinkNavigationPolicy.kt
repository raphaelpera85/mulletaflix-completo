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

/**
 * Whether a shared link's item belongs to the server this session is signed in to.
 *
 * `ShareItemContent` writes the generating server's id into the link precisely so
 * a recipient on another server does not resolve the id against their own
 * library. That value used to be parsed and then discarded, so a link from
 * another server opened an unrelated item or none at all.
 *
 * An unknown id on either side means "cannot tell", and the link is allowed: a
 * server that does not report its id must not lose link support.
 */
internal fun shouldOpenLinkOnCurrentServer(
    linkServerId: String?,
    sessionServerId: String?,
): Boolean {
    val link = linkServerId?.trim().orEmpty()
    val session = sessionServerId?.trim().orEmpty()
    if (link.isEmpty() || session.isEmpty()) return true
    return link.equals(session, ignoreCase = true)
}

/**
 * A link from another server needs the existing server-switch/login flow.
 * Auth routes are left alone so the pending request survives until login.
 */
internal fun shouldRedirectToServerSelectionForDeepLinkMismatch(
    currentRoute: String?,
    linkServerId: String?,
    sessionServerId: String?,
): Boolean {
    if (shouldOpenLinkOnCurrentServer(linkServerId, sessionServerId)) return false
    return currentRoute != MulletaFlixRoute.SERVER_SELECTION && currentRoute != MulletaFlixRoute.LOGIN
}
