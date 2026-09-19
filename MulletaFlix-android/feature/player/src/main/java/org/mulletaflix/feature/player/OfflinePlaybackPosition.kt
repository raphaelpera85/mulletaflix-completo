package org.mulletaflix.feature.player

import java.security.MessageDigest

/** Stable, non-sensitive preference key for a downloaded media URI.
 *
 * Keep the unnamespaced input for compatibility with positions written by v1.0.25.
 */
internal fun offlinePlaybackPositionKey(uri: String): String =
    stablePlaybackPositionKey(uri)

/** Stable, user-scoped key for a remote item whose progress may not be synced yet. */
internal fun remotePlaybackPositionKey(userId: String, itemId: String): String =
    stablePlaybackPositionKey("remote:$userId:$itemId")

private fun stablePlaybackPositionKey(value: String): String {
    val digest = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(Charsets.UTF_8))
    return "position_" + digest.joinToString("") { byte -> "%02x".format(byte) }
}
