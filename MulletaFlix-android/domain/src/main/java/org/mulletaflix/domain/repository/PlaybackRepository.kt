package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.MediaSource

data class PlaybackInfo(
    val playSessionId: String,
    val mediaSources: List<MediaSource>,
)

interface PlaybackRepository {
    suspend fun getPlaybackInfo(
        itemId: String,
        userId: String,
        audioStreamIndex: Int? = null,
        subtitleStreamIndex: Int? = null,
        startTimeTicks: Long? = null,
    ): Result<PlaybackInfo>

    suspend fun reportPlaybackStart(
        itemId: String,
        playSessionId: String?,
        mediaSourceId: String?,
        audioIndex: Int?,
        subtitleIndex: Int?,
        positionTicks: Long,
    ): Result<Unit>

    suspend fun reportPlaybackProgress(
        itemId: String,
        playSessionId: String?,
        mediaSourceId: String?,
        audioIndex: Int?,
        subtitleIndex: Int?,
        positionTicks: Long,
        isPaused: Boolean,
    ): Result<Unit>

    suspend fun reportPlaybackStopped(
        itemId: String,
        playSessionId: String?,
        mediaSourceId: String?,
        positionTicks: Long,
    ): Result<Unit>

    suspend fun getMediaSegments(itemId: String): Result<List<org.mulletaflix.domain.model.MediaSegment>> = Result.success(emptyList())
}
