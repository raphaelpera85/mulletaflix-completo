package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

/**
 * UseCase to toggle the played/watched state of a media item.
 * Returns the updated played state on success.
 */
class TogglePlayedUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
) {
    suspend operator fun invoke(userId: String, itemId: String, currentPlayed: Boolean): Result<Boolean> {
        val target = !currentPlayed
        val op = if (target) {
            mediaRepository.markAsPlayed(userId, itemId)
        } else {
            mediaRepository.markAsUnplayed(userId, itemId)
        }
        return op.map { target }
    }
}
