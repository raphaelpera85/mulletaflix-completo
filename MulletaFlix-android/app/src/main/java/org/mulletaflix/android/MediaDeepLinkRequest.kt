package org.mulletaflix.android

import android.content.Intent
import android.net.Uri
import org.mulletaflix.android.navigation.MulletaFlixRoute

/**
 * A pending media deep link, tagged with the request that produced it.
 *
 * The item id alone cannot be the delivery key: `onNewIntent` for the *same*
 * link produces the same id, so a state holder that only stores the id never
 * notifies its observers and the second tap is silently dropped. A monotonic
 * [sequence] makes every delivered request distinct.
 */
data class MediaDeepLinkRequest internal constructor(
    val itemId: String,
    val sequence: Long,
) {
    /** Destination to open once the session is usable. */
    internal val detailRoute: String get() = MulletaFlixRoute.itemDetail(itemId)
}

/**
 * Builds the pending request for [intent], or null when it carries no media
 * link. [sequence] must come from a monotonic counter owned by the activity.
 */
internal fun mediaDeepLinkRequest(intent: Intent?, sequence: Long): MediaDeepLinkRequest? {
    val itemId = extractMediaItemId(intent?.data) ?: return null
    return MediaDeepLinkRequest(itemId = itemId, sequence = sequence)
}

/** Kept for callers that only need the raw id (tests, diagnostics). */
internal fun mediaDeepLinkItemId(uri: Uri?): String? = extractMediaItemId(uri)
