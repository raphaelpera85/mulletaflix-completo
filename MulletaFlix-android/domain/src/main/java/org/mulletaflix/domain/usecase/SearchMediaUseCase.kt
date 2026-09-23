package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.SearchRepository
import org.mulletaflix.domain.repository.SearchResults
import javax.inject.Inject

/**
 * UseCase executing universal search across items, movies, and series.
 *
 * Devolve [SearchResults] e não uma lista solta: o total que o servidor informa é
 * justamente o que diz se a lista mostrada está truncada.
 */
class SearchMediaUseCase @Inject constructor(
    private val searchRepository: SearchRepository,
) {
    suspend operator fun invoke(
        userId: String,
        query: String,
        itemTypes: String? = null,
        startIndex: Int = 0,
    ): Result<SearchResults> = runCatching {
        val trimmed = query.trim()
        if (trimmed.isEmpty()) return@runCatching SearchResults(items = emptyList(), totalMatching = null)
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        searchRepository.searchItems(
            term = trimmed,
            userId = userId,
            itemTypes = itemTypes,
            startIndex = startIndex.coerceAtLeast(0),
        ).getOrThrow()
    }
}
