package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.PlaybackInfoRequestDto
import org.mulletaflix.core.api.dto.PlaybackProgressInfoDto
import org.mulletaflix.core.api.dto.PlaybackStartInfoDto
import org.mulletaflix.core.api.dto.PlaybackStopInfoDto
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.repository.PlaybackInfo
import org.mulletaflix.domain.repository.PlaybackRepository
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.first

@Singleton
class PlaybackRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
    private val sessionRepository: SessionRepository,
) : PlaybackRepository {

    override suspend fun getPlaybackInfo(
        itemId: String,
        userId: String,
        audioStreamIndex: Int?,
        subtitleStreamIndex: Int?,
        startTimeTicks: Long?,
    ): Result<PlaybackInfo> = runCatching {
        val request = PlaybackInfoRequestDto(
            userId = userId,
            audioStreamIndex = audioStreamIndex,
            subtitleStreamIndex = subtitleStreamIndex,
            startTimeTicks = startTimeTicks,
            enableDirectPlay = true,
            enableDirectStream = true,
            enableTranscoding = true,
        )
        val response = api.getPlaybackInfo(itemId = itemId, userId = userId, body = request)
        val baseUrl = sessionRepository.getBaseUrl().first().trimEnd('/')
        val token = sessionRepository.getAccessToken().first()
        PlaybackInfo(
            playSessionId = response.playSessionId ?: "",
            mediaSources = response.mediaSources.map { source ->
                val sourceId = source.id.orEmpty()
                val query = buildString {
                    append("MediaSourceId=").append(sourceId)
                    token?.takeIf { it.isNotBlank() }?.let { append("&api_key=").append(java.net.URLEncoder.encode(it, Charsets.UTF_8.name())) }
                }
                source.toDomain().copy(
                    directStreamUrl = "$baseUrl/Videos/$itemId/stream?Static=true&$query",
                    transcodeUrl = "$baseUrl/Videos/$itemId/master.m3u8?$query",
                )
            },
        )
    }

    override suspend fun reportPlaybackStart(
        itemId: String,
        playSessionId: String?,
        mediaSourceId: String?,
        audioIndex: Int?,
        subtitleIndex: Int?,
        positionTicks: Long,
    ): Result<Unit> = runCatching {
        api.reportPlaybackStart(
            PlaybackStartInfoDto(
                itemId = itemId,
                playSessionId = playSessionId,
                mediaSourceId = mediaSourceId,
                audioStreamIndex = audioIndex,
                subtitleStreamIndex = subtitleIndex,
                positionTicks = positionTicks,
            )
        )
    }

    override suspend fun reportPlaybackProgress(
        itemId: String,
        playSessionId: String?,
        mediaSourceId: String?,
        audioIndex: Int?,
        subtitleIndex: Int?,
        positionTicks: Long,
        isPaused: Boolean,
    ): Result<Unit> = runCatching {
        api.reportPlaybackProgress(
            PlaybackProgressInfoDto(
                itemId = itemId,
                playSessionId = playSessionId,
                mediaSourceId = mediaSourceId,
                audioStreamIndex = audioIndex,
                subtitleStreamIndex = subtitleIndex,
                positionTicks = positionTicks,
                isPaused = isPaused,
            )
        )
    }

    override suspend fun reportPlaybackStopped(
        itemId: String,
        playSessionId: String?,
        mediaSourceId: String?,
        positionTicks: Long,
    ): Result<Unit> = runCatching {
        api.reportPlaybackStopped(
            PlaybackStopInfoDto(
                itemId = itemId,
                playSessionId = playSessionId,
                mediaSourceId = mediaSourceId,
                positionTicks = positionTicks,
            )
        )
    }
}
