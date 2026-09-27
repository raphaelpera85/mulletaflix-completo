package org.mulletaflix.feature.player

import javax.inject.Inject
import kotlinx.coroutines.flow.first
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.playbackIssueScopeHash
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.QueuedPlaybackIssue
import org.mulletaflix.domain.repository.UserFeedbackRepository
import java.util.UUID

internal enum class PlaybackIssueReportFailureReason {
    SIGN_IN_REQUIRED,
    SESSION_CHANGED,
}

internal class PlaybackIssueReportException(
    val reason: PlaybackIssueReportFailureReason,
) : IllegalStateException()

internal fun playbackIssueReportFailureMessage(failure: Throwable): String = when (
    (failure as? PlaybackIssueReportException)?.reason
) {
    PlaybackIssueReportFailureReason.SIGN_IN_REQUIRED -> "Conecte-se à sua conta para enviar o relato."
    PlaybackIssueReportFailureReason.SESSION_CHANGED -> "Sua conexão mudou. Reabra a mídia e tente novamente."
    null -> "Não foi possível enviar o relato. Verifique a conexão e tente novamente."
}

/** Keeps playback issue reports bound to the authenticated server/account captured at submit time. */
class PlaybackIssueReporter @Inject constructor(
    private val sessionRepository: SessionRepository,
    private val userFeedbackRepository: UserFeedbackRepository,
    private val playbackIssueQueue: PlaybackIssueQueue,
) {
    suspend fun submit(itemId: String, category: String, description: String?): Result<Unit> {
        return try {
            val session = validatedSession() ?: throw PlaybackIssueReportException(
                PlaybackIssueReportFailureReason.SIGN_IN_REQUIRED,
            )
            userFeedbackRepository.reportPlaybackIssue(
                session = session,
                itemId = itemId,
                category = category,
                description = description?.trim()?.take(1_000)?.ifBlank { null },
            )
        } catch (cancelled: kotlinx.coroutines.CancellationException) {
            throw cancelled
        } catch (failure: Exception) {
            Result.failure(failure)
        }
    }

    /** Persists an offline report without retaining the URL, access token, or device ID. */
    suspend fun queueOffline(itemId: String, category: String, description: String?): Result<Unit> = try {
        val session = validatedSession() ?: throw PlaybackIssueReportException(
            PlaybackIssueReportFailureReason.SIGN_IN_REQUIRED,
        )
        playbackIssueQueue.enqueue(
            QueuedPlaybackIssue(
                id = UUID.randomUUID().toString(),
                scopeHash = session.playbackIssueScopeHash(),
                itemId = itemId,
                category = category.trim().take(100),
                description = description?.trim()?.take(1_000)?.ifBlank { null },
                createdAtEpochMillis = System.currentTimeMillis(),
            ),
        )
        Result.success(Unit)
    } catch (cancelled: kotlinx.coroutines.CancellationException) {
        throw cancelled
    } catch (failure: Exception) {
        Result.failure(failure)
    }

    private suspend fun validatedSession() = sessionRepository.getFeedbackRequestSession().first()?.also { session ->
        if (sessionRepository.getFeedbackRequestSession().first() != session) {
            throw PlaybackIssueReportException(PlaybackIssueReportFailureReason.SESSION_CHANGED)
        }
    }
}
