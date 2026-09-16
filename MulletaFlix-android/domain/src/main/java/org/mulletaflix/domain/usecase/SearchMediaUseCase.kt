package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.SearchRepository
import javax.inject.Inject

/**
 * UseCase executing universal search across items, movies, and series.
 */
class SearchMediaUseCase @Inject constructor(
    private val searchRepository: SearchRepository,
) {
    suspend operator fun invoke(
        userId: String,
        query: String,
        itemTypes: String? = null,
    ): Result<List<MediaItem>> = runCatching {
        val trimmed = query.trim()
        if (trimmed.isEmpty()) return@runCatching emptyList()
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        searchRepository.searchItems(
            term = trimmed,
            userId = userId,
            itemTypes = itemTypes,
        ).getOrThrow()
    }
}
