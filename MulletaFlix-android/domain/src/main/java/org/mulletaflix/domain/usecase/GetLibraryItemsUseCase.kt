package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

/**
 * UseCase to retrieve paginated items from a library with filtering and sorting.
 */
class GetLibraryItemsUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
) {
    suspend operator fun invoke(
        userId: String,
        libraryId: String,
        includeItemTypes: String? = null,
        sortBy: String? = null,
        sortOrder: String? = null,
        startIndex: Int = 0,
        limit: Int = 40,
        isPlayed: Boolean? = null,
        isFavorite: Boolean? = null,
        genres: String? = null,
        years: String? = null,
        officialRatings: String? = null,
    ): Result<Pair<List<MediaItem>, Int>> = runCatching {
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        require(libraryId.isNotBlank()) { "O identificador da biblioteca é obrigatório." }
        require(startIndex >= 0) { "O índice inicial não pode ser negativo." }
        require(limit > 0) { "O limite de itens deve ser maior que zero." }

        mediaRepository.getItems(
            userId = userId,
            parentId = libraryId,
            includeItemTypes = includeItemTypes,
            sortBy = sortBy,
            sortOrder = sortOrder,
            startIndex = startIndex,
            limit = limit,
            isPlayed = isPlayed,
            isFavorite = isFavorite,
            genres = genres,
            years = years,
            officialRatings = officialRatings,
        ).getOrThrow()
    }
}
