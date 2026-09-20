package org.mulletaflix.android.service

/** Stable request identity that prevents two accounts from sharing one Media3 download slot. */
internal fun scopedDownloadRequestId(userId: String, itemId: String): String =
    "${userId.trim()}::$itemId"

internal fun downloadBelongsToUser(ownerUserId: String?, currentUserId: String?): Boolean =
    !ownerUserId.isNullOrBlank() && ownerUserId == currentUserId

internal fun publicDownloadItemId(requestId: String, userId: String): String =
    requestId.removePrefix("${userId.trim()}::")
