package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.repository.SEARCH_PAGE_SIZE
import org.mulletaflix.domain.repository.SearchHintItem
import org.mulletaflix.domain.repository.SearchRepository
import org.mulletaflix.domain.repository.SearchResults
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SearchRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : SearchRepository {

    override suspend fun searchHints(term: String, userId: String?): Result<List<SearchHintItem>> = suspendRunCatching {
        api.searchHints(searchTerm = term, userId = userId)
            .searchHints
            .map {
                SearchHintItem(
                    id = it.itemId,
                    name = it.name,
                    type = it.type,
                    year = it.productionYear,
                    imageTag = it.primaryImageTag,
                )
            }
    }

    override suspend fun searchItems(
        term: String,
        userId: String,
        itemTypes: String?,
        startIndex: Int,
    ): Result<SearchResults> = suspendRunCatching {
        val response = api.getItems(
            userId = userId,
            searchTerm = term,
            includeItemTypes = itemTypes,
            startIndex = startIndex,
            // Um teto só, definido em `:domain` junto do contrato: era um `30` aqui e
            // outro lá, e a tela precisava saber o mesmo número para dizer "mostrando N
            // de M".
            limit = SEARCH_PAGE_SIZE,
        )
        val items = response.items.map { it.toDomain() }
        SearchResults(
            items = items,
            // `TotalRecordCount` é opcional no protocolo e o DTO cai para 0 quando ele
            // falta. Um total menor que o já paginado não é uma contagem, é uma
            // contradição — e aí "não sei" é mais honesto do que afirmar 0, que a tela
            // leria como "não há mais nada".
            totalMatching = response.totalRecordCount.takeIf { it >= startIndex + items.size },
        )
    }
}
