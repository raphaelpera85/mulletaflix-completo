package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.MediaSource

data class PlaybackInfo(
    val playSessionId: String,
    val mediaSources: List<MediaSource>,
    /**
     * Preenchido quando o servidor **recusou** preparar a mídia e disse por quê.
     * Ver [DEFAULT_PLAYBACK_PREPARATION_FAILURE].
     */
    val preparationError: String? = null,
) {
    /**
     * A frase que a tela deve mostrar quando não há fonte para tocar.
     *
     * Fica no tipo, e não no chamador, porque assim a mensagem específica do
     * servidor não pode ser perdida por um `?:` esquecido em cada tela.
     */
    val unavailableMessage: String
        get() = preparationError ?: DEFAULT_PLAYBACK_PREPARATION_FAILURE
}

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
