package org.mulletaflix.android.service

/** Separator between the owning account and the media id in a Media3 request id. */
private const val OWNER_SEPARATOR = "::"

/** Stable request identity that prevents two accounts from sharing one Media3 download slot. */
internal fun scopedDownloadRequestId(userId: String, itemId: String): String =
    "${userId.trim()}$OWNER_SEPARATOR$itemId"

internal fun downloadBelongsToUser(ownerUserId: String?, currentUserId: String?): Boolean =
    !ownerUserId.isNullOrBlank() && ownerUserId == currentUserId

/**
 * True for a download created before request ids were scoped to an account.
 *
 * Releases up to 1.0.6 stored the raw media id as the Media3 request id and
 * wrote no `owner:` metadata. [downloadBelongsToUser] then filters such an entry
 * out forever: it never appears in the list, so it can never be resumed or
 * removed, and "clear completed" cannot reach it either — the bytes stay on disk
 * with no way to reclaim them from inside the app.
 *
 * The signed-in account adopts them once, on first sight, so the space becomes
 * manageable again. A scoped id always contains [OWNER_SEPARATOR].
 */
internal fun isLegacyUnscopedDownload(requestId: String, ownerUserId: String?): Boolean =
    ownerUserId.isNullOrBlank() && !requestId.contains(OWNER_SEPARATOR)

/**
 * Recovers the public media id from a scoped request id.
 *
 * The account id is trimmed on **both** sides of the contract. [scopedDownloadRequestId]
 * already trims, so an untrimmed `userId` here would leave the prefix in place and
 * return the whole scoped id as if it were a media id — a silent mismatch instead
 * of a visible failure.
 */
internal fun publicDownloadItemId(requestId: String, userId: String): String =
    requestId.removePrefix("${userId.trim()}$OWNER_SEPARATOR")
