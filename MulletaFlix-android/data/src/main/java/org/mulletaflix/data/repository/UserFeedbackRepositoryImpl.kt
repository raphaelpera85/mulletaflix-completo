package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.MediaRequestDto
import org.mulletaflix.core.api.dto.PlaybackIssueDto
import org.mulletaflix.domain.repository.UserFeedbackRepository
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class UserFeedbackRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : UserFeedbackRepository {
    override suspend fun requestMedia(title: String, mediaType: String, year: Int?, notes: String?): Result<Unit> =
        suspendRunCatching { api.requestMedia(MediaRequestDto(title, mediaType, year, notes)) }

    override suspend fun reportPlaybackIssue(itemId: String, category: String, description: String?): Result<Unit> =
        suspendRunCatching { api.reportPlaybackIssue(PlaybackIssueDto(itemId, category, description)) }
}
