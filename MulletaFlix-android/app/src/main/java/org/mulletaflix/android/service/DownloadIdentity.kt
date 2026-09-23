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
 * Adoption must only happen for a **known** account. `observeDownloads` emits a
 * snapshot before the session has been read, when the cached account id is still
 * null; adopting there would persist an empty owner and make the entry
 * unreachable for everyone, which is worse than leaving it for the next snapshot.
 */
internal fun shouldAdoptLegacyDownload(requestId: String, ownerUserId: String?, currentUserId: String?): Boolean =
    isLegacyUnscopedDownload(requestId, ownerUserId) && !currentUserId.isNullOrBlank()

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

/**
 * The Media3 request id an **already existing** entry is stored under, or null.
 *
 * Two shapes can be in the local index for the same media: the raw media id, as
 * written by releases up to 1.0.6, and the account-scoped id written since. Every
 * operation that touches an existing entry therefore has to ask which of the two
 * is there, instead of assuming the current shape. `remove` asked and `retry` did
 * not: retrying a legacy download wrote a *second* slot under the scoped id, left
 * the failed one in the list, and fetched the file twice. Sharing this one
 * resolver is what removes the possibility of the two disagreeing again.
 *
 * Returning null rather than falling back to the raw id matters too: a raw id
 * that is not in the index would be added as a brand-new, owner-less download,
 * which is the shape [shouldAdoptLegacyDownload] then hands to whoever signs in
 * first.
 */
internal fun existingDownloadRequestId(
    userId: String,
    itemId: String,
    isInIndex: (String) -> Boolean,
): String? {
    val scopedId = scopedDownloadRequestId(userId, itemId)
    return when {
        isInIndex(scopedId) -> scopedId
        isInIndex(itemId) -> itemId
        else -> null
    }
}
