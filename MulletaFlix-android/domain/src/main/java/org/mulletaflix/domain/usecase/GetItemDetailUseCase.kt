package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

/**
 * UseCase to retrieve detailed information for a specific media item.
 */
class GetItemDetailUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
) {
    suspend operator fun invoke(userId: String, itemId: String): Result<MediaItem> {
        val cleanUser = userId.trim()
        if (cleanUser.isBlank()) {
            return Result.failure(IllegalArgumentException("O ID do usuário não pode estar vazio."))
        }
        val cleanItem = itemId.trim()
        if (cleanItem.isBlank()) {
            return Result.failure(IllegalArgumentException("O ID do item não pode estar vazio."))
        }
        return mediaRepository.getItem(cleanUser, cleanItem)
    }
}
