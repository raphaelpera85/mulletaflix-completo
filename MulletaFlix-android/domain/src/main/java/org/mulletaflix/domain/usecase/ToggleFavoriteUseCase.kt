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
        val cleanUser = userId.trim()
        if (cleanUser.isEmpty()) {
            return Result.failure(IllegalArgumentException("O identificador do usuário é obrigatório."))
        }
        val cleanItem = itemId.trim()
        if (cleanItem.isEmpty()) {
            return Result.failure(IllegalArgumentException("O identificador do item é obrigatório."))
        }
        val target = !currentFavorite
        val op = if (target) {
            mediaRepository.markAsFavorite(cleanUser, cleanItem)
        } else {
            mediaRepository.unmarkAsFavorite(cleanUser, cleanItem)
        }
        return op.map { target }
    }
}
