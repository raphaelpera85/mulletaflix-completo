package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow

interface SearchHistoryRepository {
    fun observeHistory(userId: String?): Flow<List<String>>
    suspend fun add(userId: String?, query: String)
    suspend fun remove(userId: String?, query: String)
    suspend fun clear(userId: String?)
}
