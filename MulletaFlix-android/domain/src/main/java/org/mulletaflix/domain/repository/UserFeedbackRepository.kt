package org.mulletaflix.domain.repository

import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.domain.model.MediaSuggestion

interface UserFeedbackRepository {
    suspend fun getMediaSuggestions(session: FeedbackRequestSession, query: String): Result<List<MediaSuggestion>> = Result.success(emptyList())
    suspend fun requestMedia(session: FeedbackRequestSession, title: String, mediaType: String, year: Int?, notes: String?): Result<Unit>
    suspend fun reportPlaybackIssue(session: FeedbackRequestSession, itemId: String, category: String, description: String?): Result<Unit>
}
