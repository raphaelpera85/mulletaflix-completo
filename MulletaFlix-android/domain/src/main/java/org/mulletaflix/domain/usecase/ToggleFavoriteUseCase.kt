package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

/**
 * UseCase to toggle the favorite state of a media item.
 * Returns the updated favorite state on success.
 */
class ToggleFavoriteUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
) {
    suspend operator fun invoke(userId: String, itemId: String, currentFavorite: Boolean): Result<Boolean> {
        val target = !currentFavorite
        val op = if (target) {
            mediaRepository.markAsFavorite(userId, itemId)
        } else {
            mediaRepository.unmarkAsFavorite(userId, itemId)
        }
        return op.map { target }
    }
}
