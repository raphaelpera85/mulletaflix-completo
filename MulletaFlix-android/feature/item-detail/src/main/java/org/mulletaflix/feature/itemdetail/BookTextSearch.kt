package org.mulletaflix.feature.itemdetail

import java.text.Normalizer
import java.util.Locale
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import org.readium.r2.shared.publication.Locator
import org.readium.r2.shared.publication.Publication
import org.readium.r2.shared.publication.services.search.SearchService

internal object BookTextSearch {
    const val MAX_RESULTS = 100

    fun search(chunks: List<String>, query: String, maxResults: Int = MAX_RESULTS): List<Int> {
        val normalizedQuery = normalize(query)
        if (normalizedQuery.isEmpty() || maxResults <= 0) return emptyList()

        return buildList(minOf(maxResults, MAX_RESULTS)) {
            for (index in chunks.indices) {
                if (normalize(chunks[index]).contains(normalizedQuery)) {
                    add(index)
                    if (size == minOf(maxResults, MAX_RESULTS)) break
                }
            }
        }
    }

    private fun normalize(value: String): String = Normalizer
        .normalize(value.trim().lowercase(Locale.ROOT), Normalizer.Form.NFD)
        .filterNot { Character.getType(it) == Character.NON_SPACING_MARK.toInt() }
}

internal data class BookPdfSearchHit(val pageIndex: Int, val snippet: String)

internal object BookPdfTextSearch {
    private const val MAX_SNIPPET_CHARACTERS = 140

    suspend fun search(
        pageCount: Int,
        query: String,
        maxResults: Int = BookTextSearch.MAX_RESULTS,
        extractPageText: suspend (Int) -> String,
    ): List<BookPdfSearchHit> {
        val limit = minOf(maxResults, BookTextSearch.MAX_RESULTS)
        if (pageCount <= 0 || query.isBlank() || limit <= 0) return emptyList()

        return buildList {
            for (pageIndex in 0 until pageCount) {
                currentCoroutineContext().ensureActive()
                val text = extractPageText(pageIndex)
                if (BookTextSearch.search(listOf(text), query, maxResults = 1).isNotEmpty()) {
                    add(BookPdfSearchHit(pageIndex, text.trim().take(MAX_SNIPPET_CHARACTERS)))
                    if (size == limit) break
                }
            }
        }
    }
}

internal suspend fun searchBookPublication(
    publication: Publication,
    query: String,
    maxResults: Int = BookTextSearch.MAX_RESULTS,
): List<Locator> {
    if (query.isBlank() || maxResults <= 0) return emptyList()
    val service = publication.findService(SearchService::class) ?: return emptyList()
    val iterator = service.search(
        query,
        SearchService.Options(caseSensitive = false, diacriticSensitive = false),
    )
    val limit = minOf(maxResults, BookTextSearch.MAX_RESULTS)
    return try {
        buildList {
            while (size < limit) {
                val result = iterator.next().getOrNull() ?: break
                addAll(result.locators.take(limit - size))
            }
        }
    } finally {
        iterator.close()
    }
}
