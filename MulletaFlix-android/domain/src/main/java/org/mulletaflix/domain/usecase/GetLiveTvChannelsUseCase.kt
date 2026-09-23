package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.LiveTvRepository
import javax.inject.Inject

/**
 * Aggregated live tv channels and active dvr recordings.
 *
 * [recordingsError] is not null when the channel list loaded but the recordings did not.
 * The failure used to be swallowed with `getOrDefault(emptyList())`, so a viewer whose
 * recordings request failed saw the section simply disappear — indistinguishable from
 * "you have no recordings".
 */
data class LiveTvGuide(
    val channels: List<MediaItem> = emptyList(),
    val recordings: List<MediaItem> = emptyList(),
    val recordingsError: String? = null,
)

/**
 * UseCase coordinating Live TV channels and recordings.
 */
class GetLiveTvChannelsUseCase @Inject constructor(
    private val liveTvRepository: LiveTvRepository,
) {
    suspend operator fun invoke(userId: String): Result<LiveTvGuide> = runCatching {
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        val channels = liveTvRepository.getChannels(userId).getOrThrow()
        val recordings = liveTvRepository.getRecordings(userId)
        LiveTvGuide(
            channels = channels,
            recordings = recordings.getOrDefault(emptyList()),
            recordingsError = recordings.exceptionOrNull()?.let { error ->
                error.localizedMessage?.takeIf(String::isNotBlank)
                    ?: "Não foi possível carregar as gravações."
            },
        )
    }
}
