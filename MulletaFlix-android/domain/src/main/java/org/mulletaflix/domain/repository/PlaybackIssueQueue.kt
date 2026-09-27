package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow

/** User-submitted issue data plus a one-way account/server scope; contains no session secrets. */
data class QueuedPlaybackIssue(
    val id: String,
    val scopeHash: String,
    val itemId: String,
    val category: String,
    val description: String?,
    val createdAtEpochMillis: Long,
)

interface PlaybackIssueQueue {
    val pendingCount: Flow<Int>
    suspend fun enqueue(issue: QueuedPlaybackIssue)
    suspend fun pending(): List<QueuedPlaybackIssue>
    suspend fun remove(id: String)
}
