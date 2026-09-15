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
                val urls = buildPlaybackStreamUrls(
                    baseUrl = baseUrl,
                    itemId = itemId,
                    mediaSourceId = sourceId,
                    accessToken = token,
                )
                source.toDomain().copy(
                    directStreamUrl = urls.directStream,
                    transcodeUrl = urls.transcode,
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

    override suspend fun getMediaSegments(itemId: String): Result<List<org.mulletaflix.domain.model.MediaSegment>> = runCatching {
        val response = api.getMediaSegments(itemId)
        response.items.mapNotNull { dto ->
            val id = dto.id ?: return@mapNotNull null
            val item = dto.itemId ?: itemId
            val type = when (dto.type?.trim()?.lowercase()) {
                "intro" -> org.mulletaflix.domain.model.MediaSegmentType.Intro
                "outro" -> org.mulletaflix.domain.model.MediaSegmentType.Outro
                "preview" -> org.mulletaflix.domain.model.MediaSegmentType.Preview
                "recap" -> org.mulletaflix.domain.model.MediaSegmentType.Recap
                "commercial" -> org.mulletaflix.domain.model.MediaSegmentType.Commercial
                else -> org.mulletaflix.domain.model.MediaSegmentType.Unknown
            }
            org.mulletaflix.domain.model.MediaSegment(
                id = id,
                itemId = item,
                type = type,
                startTicks = dto.startTicks,
                endTicks = dto.endTicks,
            )
        }
    }
}
