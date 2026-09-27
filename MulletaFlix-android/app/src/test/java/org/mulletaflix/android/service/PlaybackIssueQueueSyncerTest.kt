package org.mulletaflix.android.service

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.core.common.session.playbackIssueScopeHash
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.QueuedPlaybackIssue
import org.mulletaflix.domain.repository.UserFeedbackRepository

class PlaybackIssueQueueSyncerTest {
    private val session = FeedbackRequestSession("https://server.test", "token", "user", "device", "server-id")

    @Test
    fun `sync sends only matching account reports and removes confirmed reports`() = runTest {
        val otherSession = session.copy(userId = "other-user")
        val queue = RecordingQueue(
            listOf(issue("matching", session), issue("other-account", otherSession)),
        )
        val feedback = RecordingFeedback()

        val outcome = PlaybackIssueQueueSyncer(queue, FakeSessionRepository { session }, feedback).sync()

        assertEquals(PlaybackIssueSyncOutcome.COMPLETE, outcome)
        assertEquals(listOf("movie-matching"), feedback.itemIds)
        assertEquals(listOf("other-account"), queue.items.map { it.id })
    }

    @Test
    fun `failed server response leaves the report queued for retry`() = runTest {
        val issue = issue("pending", session)
        val queue = RecordingQueue(listOf(issue))
        val feedback = RecordingFeedback(Result.failure(IllegalStateException("network")))

        val outcome = PlaybackIssueQueueSyncer(queue, FakeSessionRepository { session }, feedback).sync()

        assertEquals(PlaybackIssueSyncOutcome.RETRY, outcome)
        assertEquals(listOf(issue), queue.items)
    }

    @Test
    fun `session switch during sync stops before sending to another account`() = runTest {
        val alternate = session.copy(serverUrl = "https://other.test", userId = "other-user")
        val queue = RecordingQueue(listOf(issue("pending", session)))
        var sessionReads = 0
        val repository = FakeSessionRepository {
            sessionReads++
            if (sessionReads == 1) session else alternate
        }
        val feedback = RecordingFeedback()

        val outcome = PlaybackIssueQueueSyncer(queue, repository, feedback).sync()

        assertEquals(PlaybackIssueSyncOutcome.SESSION_CHANGED, outcome)
        assertTrue(feedback.itemIds.isEmpty())
        assertEquals(1, queue.items.size)
    }

    private fun issue(id: String, owner: FeedbackRequestSession) = QueuedPlaybackIssue(
        id = id,
        scopeHash = owner.playbackIssueScopeHash(),
        itemId = "movie-$id",
        category = "Travamentos",
        description = null,
        createdAtEpochMillis = 1L,
    )
}

private class RecordingQueue(items: List<QueuedPlaybackIssue>) : PlaybackIssueQueue {
    override val pendingCount: Flow<Int> = flowOf(items.size)
    val items = items.toMutableList()
    override suspend fun enqueue(issue: QueuedPlaybackIssue) { items += issue }
    override suspend fun pending(): List<QueuedPlaybackIssue> = items.toList()
    override suspend fun remove(id: String) { items.removeAll { it.id == id } }
}

private class FakeSessionRepository(
    private val active: () -> FeedbackRequestSession?,
) : SessionRepository {
    override fun getAccessToken(): Flow<String?> = flowOf(active()?.accessToken)
    override fun getDeviceId(): Flow<String> = flowOf(active()?.deviceId.orEmpty())
    override fun getBaseUrl(): Flow<String> = flowOf(active()?.serverUrl.orEmpty())
    override fun getCurrentUserId(): Flow<String?> = flowOf(active()?.userId)
    override fun getFeedbackRequestSession(): Flow<FeedbackRequestSession?> = flowOf(active())
    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
    override suspend fun setBaseUrl(url: String) = Unit
    override suspend fun clearSession() = Unit
}

private class RecordingFeedback(
    private val result: Result<Unit> = Result.success(Unit),
) : UserFeedbackRepository {
    val itemIds = mutableListOf<String>()
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
        itemIds += itemId
        return result
    }
}
