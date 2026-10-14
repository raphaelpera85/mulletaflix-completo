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
        val cleanUser = userId.trim()
        if (cleanUser.isEmpty()) {
            return Result.failure(IllegalArgumentException("O identificador do usuário é obrigatório."))
        }
        val cleanItem = itemId.trim()
        if (cleanItem.isEmpty()) {
            return Result.failure(IllegalArgumentException("O identificador do item é obrigatório."))
        }
        val target = !currentPlayed
        val op = if (target) {
            mediaRepository.markAsPlayed(cleanUser, cleanItem)
        } else {
            mediaRepository.markAsUnplayed(cleanUser, cleanItem)
        }
        return op.map { target }
    }
}
