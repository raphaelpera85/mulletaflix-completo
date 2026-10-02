package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

/** Loads the authenticated user's complete server-side favorites list. */
class GetFavoriteItemsUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
) {
    suspend operator fun invoke(
        userId: String,
        startIndex: Int = 0,
        limit: Int = 40,
        sortBy: String = "SortName",
        sortOrder: String = "Ascending",
        searchTerm: String? = null,
    ): Result<Pair<List<MediaItem>, Int>> = runCatching {
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        require(startIndex >= 0) { "O índice inicial não pode ser negativo." }
        require(limit > 0) { "O limite de itens deve ser maior que zero." }

        mediaRepository.getItems(
            userId = userId,
            sortBy = sortBy,
            sortOrder = sortOrder,
            filters = "IsFavorite",
            searchTerm = searchTerm?.trim()?.takeIf(String::isNotEmpty),
            startIndex = startIndex,
            limit = limit,
            isFavorite = true,
        ).getOrThrow()
    }
}
