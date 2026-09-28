package org.mulletaflix.android.service

import org.mulletaflix.domain.repository.DownloadMediaMetadata
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import java.io.IOException
import java.net.ConnectException
import java.net.SocketException
import java.net.SocketTimeoutException
import java.net.UnknownHostException
import javax.net.ssl.SSLException

internal const val MAX_OFFLINE_SUBTITLE_ATTEMPTS = 3

internal fun shouldRetryOfflineSubtitleRecoveryWork(
    sessionChanged: Boolean,
    hasTransientFailures: Boolean,
): Boolean = sessionChanged || hasTransientFailures

internal fun shouldRetryOfflineSubtitleHttpStatus(statusCode: Int): Boolean =
    statusCode == 408 || statusCode == 429 || statusCode in 500..599

internal fun isTransientOfflineSubtitleNetworkFailure(failure: IOException): Boolean =
    failure !is SSLException && (
        failure is SocketTimeoutException ||
            failure is ConnectException ||
            failure is UnknownHostException ||
            failure is SocketException
        )

internal fun offlineSubtitleRetryDelayMillis(failedAttempt: Int): Long {
    require(failedAttempt in 1 until MAX_OFFLINE_SUBTITLE_ATTEMPTS)
    return 1_000L shl (failedAttempt - 1)
}

internal data class OfflineSubtitleRecoveryCandidate(
    val requestId: String,
    val itemId: String,
    val ownerUserId: String?,
    val media: DownloadMediaMetadata,
)

/** Select missing sidecars only for the authenticated account and server that own each download. */
internal fun selectOfflineSubtitleRecoveries(
    candidates: List<OfflineSubtitleRecoveryCandidate>,
    activeUserId: String,
    activeServerScope: String,
    hasStoredSubtitle: (OfflineSubtitleRecoveryCandidate, DownloadSubtitleMetadata) -> Boolean,
): List<OfflineSubtitleRecoveryCandidate> {
    if (activeUserId.isBlank() || activeServerScope.isBlank()) return emptyList()

    return candidates.mapNotNull { candidate ->
        if (candidate.requestId.isBlank() || candidate.itemId.isBlank()) return@mapNotNull null
        if (candidate.ownerUserId != activeUserId || candidate.media.serverId != activeServerScope) {
            return@mapNotNull null
        }
        val missingSubtitles = candidate.media.subtitles.filterNot { subtitle ->
            hasStoredSubtitle(candidate, subtitle)
        }
        if (missingSubtitles.isEmpty()) return@mapNotNull null
        candidate.copy(media = candidate.media.copy(subtitles = missingSubtitles))
    }
}
