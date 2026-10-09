package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow
import java.net.URI
import java.util.Locale

data class SearchHistoryScope(
    val serverId: String?,
    val serverUrl: String,
    val userId: String?,
) {
    val serverIdentity: String
        get() {
            val id = serverId?.trim()?.takeIf(String::isNotEmpty)
            return if (id != null) "id:${id.lowercase(Locale.ROOT)}"
            else "url:${normalizedServerUrl(serverUrl)}"
        }

    val userIdentity: String
        get() = userId?.trim()?.takeIf(String::isNotEmpty) ?: "anonymous"

    /** Stable across public/LAN URLs when the server supplies its persistent identity. */
    val identity: String
        get() = "${serverIdentity.length}:$serverIdentity|${userIdentity.length}:$userIdentity"
}

private fun normalizedServerUrl(value: String): String {
    val raw = value.trim()
    if (raw.isEmpty()) return "unconfigured"
    val uri = runCatching { URI(raw).normalize() }.getOrNull()
        ?: return raw.trimEnd('/').lowercase(Locale.ROOT)
    val scheme = uri.scheme?.lowercase(Locale.ROOT)
        ?: return raw.trimEnd('/').lowercase(Locale.ROOT)
    val host = uri.host?.lowercase(Locale.ROOT)
        ?: return raw.trimEnd('/').lowercase(Locale.ROOT)
    val port = uri.port.takeUnless {
        it < 0 ||
        (scheme == "https" && it == 443) || (scheme == "http" && it == 80)
    }?.let { ":$it" }.orEmpty()
    val path = uri.rawPath.orEmpty().trimEnd('/')
    val query = uri.rawQuery?.let { "?$it" }.orEmpty()
    return "$scheme://$host$port$path$query"
}

interface SearchHistoryRepository {
    fun observeHistory(scope: SearchHistoryScope): Flow<List<String>>
    suspend fun add(scope: SearchHistoryScope, query: String)
    suspend fun remove(scope: SearchHistoryScope, query: String)
    suspend fun clear(scope: SearchHistoryScope)
    suspend fun clearAllForUser(userId: String?)
}
