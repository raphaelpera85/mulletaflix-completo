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
    private const val PDF_SNIPPET_CHARACTERS = 140

    suspend fun search(chunks: List<String>, query: String, maxResults: Int = MAX_RESULTS): List<Int> {
        val normalizedQuery = normalize(query)
        if (normalizedQuery.isEmpty() || maxResults <= 0) return emptyList()
        val coroutineContext = currentCoroutineContext()

        return buildList(minOf(maxResults, MAX_RESULTS)) {
            for (index in chunks.indices) {
                coroutineContext.ensureActive()
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

    fun contextualSnippet(text: String, query: String): String {
        val normalizedQuery = normalize(query)
        if (normalizedQuery.isEmpty()) return text.take(PDF_SNIPPET_CHARACTERS)

        val normalizedText = normalizeWithSourceOffsets(text)
        val matchStart = normalizedText.value.indexOf(normalizedQuery)
        if (matchStart < 0) return text.take(PDF_SNIPPET_CHARACTERS)

        val originalMatchStart = normalizedText.sourceStarts[matchStart]
        val originalMatchEnd = normalizedText.sourceEnds[matchStart + normalizedQuery.length - 1]
        val contentLimit = PDF_SNIPPET_CHARACTERS - 4
        val matchLength = originalMatchEnd - originalMatchStart
        var start = (originalMatchStart - ((contentLimit - matchLength).coerceAtLeast(0) / 2))
            .coerceAtLeast(0)
        var end = minOf(text.length, start + contentLimit)
        if (end < originalMatchEnd) {
            end = originalMatchEnd.coerceAtMost(text.length)
            start = (end - contentLimit).coerceAtLeast(0)
        }

        if (start > 0 && Character.isLowSurrogate(text[start])) start--
        if (end < text.length && end > 0 && Character.isHighSurrogate(text[end - 1])) end--

        val rawSnippet = text.substring(start, end)
        val contentStart = rawSnippet.indexOfFirst { !it.isWhitespace() }.coerceAtLeast(0)
        val contentEnd = (rawSnippet.indexOfLast { !it.isWhitespace() } + 1).coerceAtLeast(contentStart)
        val content = rawSnippet.substring(contentStart, contentEnd)

        return buildString(PDF_SNIPPET_CHARACTERS) {
            if (start > 0 || contentStart > 0) append('…')
            append(content)
            if (end < text.length || contentEnd < rawSnippet.length) append('…')
        }
    }

    private fun normalizeWithSourceOffsets(value: String): NormalizedText {
        val normalized = StringBuilder(value.length)
        var sourceStarts = IntArray(value.length)
        var sourceEnds = IntArray(value.length)
        var mappedCharacterCount = 0
        var sourceStart = 0

        while (sourceStart < value.length) {
            val codePoint = value.codePointAt(sourceStart)
            val sourceEnd = sourceStart + Character.charCount(codePoint)
            val normalizedCodePoint = Normalizer.normalize(
                String(Character.toChars(codePoint)).lowercase(Locale.ROOT),
                Normalizer.Form.NFD,
            ).filterNot { Character.getType(it) == Character.NON_SPACING_MARK.toInt() }

            normalizedCodePoint.forEach { character ->
                if (mappedCharacterCount == sourceStarts.size) {
                    val expandedSize = maxOf(1, sourceStarts.size * 2)
                    sourceStarts = sourceStarts.copyOf(expandedSize)
                    sourceEnds = sourceEnds.copyOf(expandedSize)
                }
                normalized.append(character)
                sourceStarts[mappedCharacterCount] = sourceStart
                sourceEnds[mappedCharacterCount] = sourceEnd
                mappedCharacterCount++
            }
            sourceStart = sourceEnd
        }

        return NormalizedText(
            normalized.toString(),
            sourceStarts.copyOf(mappedCharacterCount),
            sourceEnds.copyOf(mappedCharacterCount),
        )
    }

    private data class NormalizedText(
        val value: String,
        val sourceStarts: IntArray,
        val sourceEnds: IntArray,
    )
}

internal data class BookPdfSearchHit(val pageIndex: Int, val snippet: String)

internal object BookPdfTextSearch {
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
                    add(BookPdfSearchHit(pageIndex, BookTextSearch.contextualSnippet(text, query)))
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
