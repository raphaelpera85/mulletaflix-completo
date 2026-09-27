package org.mulletaflix.android.service

import kotlinx.coroutines.flow.first
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.playbackIssueScopeHash
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.UserFeedbackRepository
import javax.inject.Inject
import javax.inject.Singleton

internal enum class PlaybackIssueSyncOutcome { COMPLETE, SESSION_REQUIRED, SESSION_CHANGED, RETRY }

/** Sends only records scoped to the current authenticated server/account; never deletes other accounts' reports. */
@Singleton
internal class PlaybackIssueQueueSyncer @Inject constructor(
    private val queue: PlaybackIssueQueue,
    private val sessionRepository: SessionRepository,
    private val feedbackRepository: UserFeedbackRepository,
) {
    suspend fun sync(): PlaybackIssueSyncOutcome = try {
        syncPendingReports()
    } catch (cancelled: kotlinx.coroutines.CancellationException) {
        throw cancelled
    } catch (_: Exception) {
        PlaybackIssueSyncOutcome.RETRY
    }

    private suspend fun syncPendingReports(): PlaybackIssueSyncOutcome {
        val reports = queue.pending()
        val initialSession = sessionRepository.getFeedbackRequestSession().first()
            ?: return PlaybackIssueSyncOutcome.SESSION_REQUIRED
        val initialScope = initialSession.playbackIssueScopeHash()
        for (report in reports.filter { it.scopeHash == initialScope }) {
            val currentSession = sessionRepository.getFeedbackRequestSession().first()
                ?: return PlaybackIssueSyncOutcome.SESSION_REQUIRED
            if (currentSession.playbackIssueScopeHash() != report.scopeHash) {
                return PlaybackIssueSyncOutcome.SESSION_CHANGED
            }
            val response = feedbackRepository.reportPlaybackIssue(
                session = currentSession,
                itemId = report.itemId,
                category = report.category,
                description = report.description,
            )
            if (response.isFailure) return PlaybackIssueSyncOutcome.RETRY
            queue.remove(report.id)
        }
        return PlaybackIssueSyncOutcome.COMPLETE
    }
}
