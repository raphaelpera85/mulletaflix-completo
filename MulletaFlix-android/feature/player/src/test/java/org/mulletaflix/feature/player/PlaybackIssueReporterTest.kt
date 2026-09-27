package org.mulletaflix.feature.player

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.core.common.session.playbackIssueScopeHash
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.QueuedPlaybackIssue

class PlaybackIssueReporterTest {
    private val activeSession = FeedbackRequestSession("https://server.test", "token", "user", "device")

    @Test
    fun `submits the active account server item category and bounded description`() = runTest {
        val feedback = RecordingFeedbackRepository()
        val reporter = PlaybackIssueReporter(FakeSessionRepository { flowOf(activeSession) }, feedback, RecordingIssueQueue())
        val longDescription = "x".repeat(1_200)

        val result = reporter.submit("movie-42", "Sem áudio", "  $longDescription  ")

        assertTrue(result.isSuccess)
        assertEquals(activeSession, feedback.session)
        assertEquals("movie-42", feedback.itemId)
        assertEquals("Sem áudio", feedback.category)
        assertEquals("x".repeat(1_000), feedback.description)
    }

    @Test
    fun `refuses to submit if no authenticated session exists`() = runTest {
        val feedback = RecordingFeedbackRepository()
        val reporter = PlaybackIssueReporter(FakeSessionRepository { flowOf(null) }, feedback, RecordingIssueQueue())

        val result = reporter.submit("movie-42", "Não reproduz", "Falha")

        assertFalse(result.isSuccess)
        assertEquals(
            PlaybackIssueReportFailureReason.SIGN_IN_REQUIRED,
            (result.exceptionOrNull() as PlaybackIssueReportException).reason,
        )
        assertEquals(
            "Conecte-se à sua conta para enviar o relato.",
            playbackIssueReportFailureMessage(checkNotNull(result.exceptionOrNull())),
        )
        assertEquals(0, feedback.calls)
    }

    @Test
    fun `refuses to submit when session changes between capture and validation`() = runTest {
        val changedSession = activeSession.copy(serverUrl = "https://other.test", userId = "other-user")
        var read = 0
        val sessionRepository = FakeSessionRepository {
            flow {
                read += 1
                emit(if (read == 1) activeSession else changedSession)
            }
        }
        val feedback = RecordingFeedbackRepository()
        val reporter = PlaybackIssueReporter(sessionRepository, feedback, RecordingIssueQueue())

        val result = reporter.submit("movie-42", "Não reproduz", "Falha")

        assertFalse(result.isSuccess)
        assertEquals(
            PlaybackIssueReportFailureReason.SESSION_CHANGED,
            (result.exceptionOrNull() as PlaybackIssueReportException).reason,
        )
        assertEquals(
            "Sua conexão mudou. Reabra a mídia e tente novamente.",
            playbackIssueReportFailureMessage(checkNotNull(result.exceptionOrNull())),
        )
        assertEquals(0, feedback.calls)
    }

    @Test
    fun `offline report persists only bounded report data and hashed session scope`() = runTest {
        val queue = RecordingIssueQueue()
        val reporter = PlaybackIssueReporter(FakeSessionRepository { flowOf(activeSession) }, RecordingFeedbackRepository(), queue)

        val result = reporter.queueOffline("movie-42", "Sem áudio", "  ${"x".repeat(1_100)}  ")

        assertTrue(result.isSuccess)
        val issue = checkNotNull(queue.issue)
        assertEquals("movie-42", issue.itemId)
        assertEquals("Sem áudio", issue.category)
        assertEquals("x".repeat(1_000), issue.description)
        assertEquals(activeSession.playbackIssueScopeHash(), issue.scopeHash)
        assertFalse(issue.scopeHash.contains(activeSession.userId))
        assertFalse(issue.scopeHash.contains(activeSession.serverUrl))
    }
}

private class RecordingIssueQueue : PlaybackIssueQueue {
    override val pendingCount: Flow<Int> = flowOf(0)
    var issue: QueuedPlaybackIssue? = null
    override suspend fun enqueue(issue: QueuedPlaybackIssue) { this.issue = issue }
    override suspend fun pending(): List<QueuedPlaybackIssue> = listOfNotNull(issue)
    override suspend fun remove(id: String) { if (issue?.id == id) issue = null }
}

private class RecordingFeedbackRepository : UserFeedbackRepository {
    var calls = 0
    var session: FeedbackRequestSession? = null
    var itemId: String? = null
    var category: String? = null
    var description: String? = null

    override suspend fun requestMedia(
        session: FeedbackRequestSession,
        title: String,
        mediaType: String,
        year: Int?,
        notes: String?,
    ): Result<Unit> = Result.success(Unit)

    override suspend fun reportPlaybackIssue(
        session: FeedbackRequestSession,
        itemId: String,
        category: String,
        description: String?,
    ): Result<Unit> {
        calls += 1
        this.session = session
        this.itemId = itemId
        this.category = category
        this.description = description
        return Result.success(Unit)
    }
}

private class FakeSessionRepository(
    private val feedbackSession: () -> Flow<FeedbackRequestSession?>,
) : SessionRepository {
    override fun getAccessToken(): Flow<String?> = flowOf(null)
    override fun getDeviceId(): Flow<String> = flowOf("device")
    override fun getBaseUrl(): Flow<String> = flowOf("https://server.test")
    override fun getCurrentUserId(): Flow<String?> = flowOf("user")
    override fun getFeedbackRequestSession(): Flow<FeedbackRequestSession?> = feedbackSession()
    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
    override suspend fun setBaseUrl(url: String) = Unit
    override suspend fun clearSession() = Unit
}
