package org.mulletaflix.feature.player

import java.security.MessageDigest

/** Stable, user-scoped preference key for a downloaded media URI. */
internal fun offlinePlaybackPositionKey(userId: String, uri: String): String =
    stablePlaybackPositionKey("offline:$userId:$uri")

/** Legacy key kept only to migrate positions written before account isolation. */
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
