package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.MediaSourceDto
import org.mulletaflix.core.api.dto.OpenLiveStreamDto
import org.mulletaflix.core.api.dto.PlaybackInfoRequestDto
import org.mulletaflix.core.api.dto.PlaybackProgressInfoDto
import org.mulletaflix.core.api.dto.PlaybackStartInfoDto
import org.mulletaflix.core.api.dto.PlaybackStopInfoDto
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.repository.PlaybackInfo
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.repository.playbackPreparationFailureMessage
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
    ): Result<PlaybackInfo> = suspendRunCatching {
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
            // O servidor recusa preparar com HTTP 200 e a lista vazia, e diz o motivo
            // em `ErrorCode`. Sem isto, "sua conta não tem permissão" e "limite de
            // transmissões simultâneas" chegavam à tela como "nenhuma fonte
            // disponível" — a mesma frase para três causas diferentes. Sem código,
            // `preparationError` fica nulo e a tela usa a frase genérica.
            // O servidor recusa preparar com HTTP 200 e a lista vazia, e diz o motivo
            // em `ErrorCode`. Sem isto, "sua conta não tem permissão" e "limite de
            // transmissões simultâneas" chegavam à tela como "nenhuma fonte
            // disponível" — a mesma frase para três causas diferentes. Sem código,
            // `preparationError` fica nulo e a tela usa a frase genérica.
            preparationError = if (response.mediaSources.isEmpty()) {
                response.errorCode?.let(::playbackPreparationFailureMessage)
            } else {
                null
            },
            mediaSources = response.mediaSources.map { source ->
                // A tuner channel has to be opened before its stream means anything; the
                // opened source carries the `LiveStreamId` the stream route uses. Doing
                // it here means every caller of playback info — the video player, the
                // live TV screen — gets a source that can actually be played.
                val playable = if (shouldOpenLiveStream(source.requiresOpening, source.liveStreamId)) {
                    openLiveStream(
                        itemId = itemId,
                        userId = userId,
                        playSessionId = response.playSessionId,
                        source = source,
                        audioStreamIndex = audioStreamIndex,
                        subtitleStreamIndex = subtitleStreamIndex,
                        startTimeTicks = startTimeTicks,
                    )
                } else {
                    source
                }
                val sourceId = playable.id.orEmpty()
                val urls = buildPlaybackStreamUrls(
                    baseUrl = baseUrl,
                    itemId = itemId,
                    mediaSourceId = sourceId,
                    accessToken = token,
                    liveStreamId = playable.liveStreamId,
                )
                playable.toDomain().copy(
                    directStreamUrl = urls.directStream,
                    transcodeUrl = urls.transcode,
                )
            },
        )
    }

    /**
     * Opens a live channel and returns the source the server handed back.
     *
     * A failure here is propagated on purpose: without the opened feed the stream route
     * answers 404 for a protocol that cannot be read as a file, and a player silently
     * pointed at a broken URL is harder to understand than an error that says the
     * channel could not be opened.
     */
    private suspend fun openLiveStream(
        itemId: String,
        userId: String,
        playSessionId: String?,
        source: MediaSourceDto,
        audioStreamIndex: Int?,
        subtitleStreamIndex: Int?,
        startTimeTicks: Long?,
    ): MediaSourceDto {
        val opened = api.openLiveStream(
            userId = userId,
            itemId = itemId,
            playSessionId = playSessionId,
            body = OpenLiveStreamDto(
                userId = userId,
                itemId = itemId,
                playSessionId = playSessionId,
                openToken = source.openToken,
                startTimeTicks = startTimeTicks,
                audioStreamIndex = audioStreamIndex,
                subtitleStreamIndex = subtitleStreamIndex,
            ),
        )
        // The opened source is authoritative for the id and for `LiveStreamId`; anything
        // the server did not send is kept from the source we asked about.
        val openedSource = opened.mediaSource
            ?: error("O servidor não devolveu a fonte do canal ao abrir o stream ao vivo.")
        return openedSource.copy(
            id = openedSource.id ?: source.id,
            liveStreamId = openedSource.liveStreamId ?: source.liveStreamId,
        )
    }

    override suspend fun reportPlaybackStart(
        itemId: String,
        playSessionId: String?,
        mediaSourceId: String?,
        audioIndex: Int?,
        subtitleIndex: Int?,
        positionTicks: Long,
    ): Result<Unit> = suspendRunCatching {
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
    ): Result<Unit> = suspendRunCatching {
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
    ): Result<Unit> = suspendRunCatching {
        api.reportPlaybackStopped(
            PlaybackStopInfoDto(
                itemId = itemId,
                playSessionId = playSessionId,
                mediaSourceId = mediaSourceId,
                positionTicks = positionTicks,
            )
        )
    }

    override suspend fun getMediaSegments(itemId: String): Result<List<org.mulletaflix.domain.model.MediaSegment>> = suspendRunCatching {
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
