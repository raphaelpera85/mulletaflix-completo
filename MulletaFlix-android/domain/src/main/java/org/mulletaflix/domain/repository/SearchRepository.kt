package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.MediaItem

data class SearchHintItem(
    val id: String,
    val name: String,
    val type: String?,
    val year: Int?,
    val imageTag: String?,
)

interface SearchRepository {
    suspend fun searchHints(term: String, userId: String?): Result<List<SearchHintItem>>
    suspend fun searchItems(term: String, userId: String, itemTypes: String? = null): Result<List<MediaItem>>
}
