package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.SearchHintItem
import org.mulletaflix.domain.repository.SearchRepository
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SearchRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : SearchRepository {

    override suspend fun searchHints(term: String, userId: String?): Result<List<SearchHintItem>> = runCatching {
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

    override suspend fun searchItems(term: String, userId: String, itemTypes: String?): Result<List<MediaItem>> = runCatching {
        api.getItems(
            userId = userId,
            searchTerm = term,
            includeItemTypes = itemTypes,
            limit = 30,
        ).items.map { it.toDomain() }
    }
}
