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
@ConsistentCopyVisibility
data class MediaDeepLinkRequest internal constructor(
    val itemId: String,
    val sequence: Long,
    /**
     * The server the link was generated on, when the sender included it.
     *
     * `ShareItemContent` writes `&serverId=` precisely so a recipient on another
     * server does not resolve the id against their own library, but the value
     * used to be parsed and then dropped: only [itemId] reached the request, so a
     * link from another server opened an unrelated item, or none at all.
     */
    val serverId: String? = null,
) {
    /** Destination to open once the session is usable. */
    internal val detailRoute: String get() = MulletaFlixRoute.itemDetail(itemId)
}

/**
 * Builds the pending request for [intent], or null when it carries no media
 * link. [sequence] must come from a monotonic counter owned by the activity.
 */
internal fun mediaDeepLinkRequest(intent: Intent?, sequence: Long): MediaDeepLinkRequest? =
    mediaDeepLinkRequest(intent?.data?.toString(), sequence)

/**
 * String form of [mediaDeepLinkRequest].
 *
 * Exists for the same reason as the string overload of the parser: `Uri` is
 * stubbed to null in JVM tests, so anything that has to be asserted without a
 * device must not go through it.
 */
internal fun mediaDeepLinkRequest(rawUri: String?, sequence: Long): MediaDeepLinkRequest? {
    val link = extractMediaLink(rawUri) ?: return null
    return MediaDeepLinkRequest(
        itemId = link.itemId,
        sequence = sequence,
        serverId = link.serverId,
    )
}

/** Kept for callers that only need the raw id (tests, diagnostics). */
internal fun mediaDeepLinkItemId(uri: Uri?): String? = extractMediaItemId(uri)
