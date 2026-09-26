package org.mulletaflix.domain.repository

interface UserFeedbackRepository {
    suspend fun requestMedia(title: String, mediaType: String, year: Int?, notes: String?): Result<Unit>
    suspend fun reportPlaybackIssue(itemId: String, category: String, description: String?): Result<Unit>
}
