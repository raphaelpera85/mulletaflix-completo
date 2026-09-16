package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.LiveTvRepository
import javax.inject.Inject

/**
 * Aggregated live tv channels and active dvr recordings.
 */
data class LiveTvGuide(
    val channels: List<MediaItem> = emptyList(),
    val recordings: List<MediaItem> = emptyList(),
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
        val recordings = liveTvRepository.getRecordings(userId).getOrDefault(emptyList())
        LiveTvGuide(channels = channels, recordings = recordings)
    }
}
