package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.unit.dp
import org.readium.r2.shared.publication.Locator
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@Composable
internal fun BookTextSearchDialog(
    search: suspend (String) -> List<BookTextSearchResult>,
    onDismiss: () -> Unit,
    onOpenResult: (BookTextSearchResult) -> Unit,
) {
    var query by rememberSaveable { mutableStateOf("") }
    val results = remember { mutableStateListOf<BookTextSearchResult>() }
    var hasSearched by remember { mutableStateOf(false) }
    var isSearching by remember { mutableStateOf(false) }
    var searchFailed by remember { mutableStateOf(false) }
    var activeQuery by remember { mutableStateOf<String?>(null) }
    var activeSearch by remember { mutableStateOf<Job?>(null) }
    var searchGeneration by remember { mutableStateOf(0) }
    val scope = rememberCoroutineScope()

    fun updateQuery(updatedQuery: String) {
        if (updatedQuery == query) return
        query = updatedQuery
        results.clear()
        hasSearched = false
        searchFailed = false
        if (isSearching) {
            searchGeneration++
            activeSearch?.cancel()
            activeSearch = null
            activeQuery = null
            isSearching = false
        }
    }

    fun search() {
        val requestedQuery = query
        if (requestedQuery.isBlank() || (isSearching && requestedQuery == activeQuery)) return
        activeSearch?.cancel()
        val generation = ++searchGeneration
        activeQuery = requestedQuery
        isSearching = true
        hasSearched = true
        searchFailed = false
        results.clear()
        activeSearch = scope.launch {
            try {
                val matches = withContext(Dispatchers.IO) { search(requestedQuery) }
                if (generation == searchGeneration) results.addAll(matches)
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Exception) {
                if (generation == searchGeneration) searchFailed = true
            } finally {
                if (generation == searchGeneration) {
                    isSearching = false
                    activeQuery = null
                    activeSearch = null
                }
            }
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Buscar no livro") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = query,
                    onValueChange = ::updateQuery,
                    label = { Text("Texto") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth().testTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG),
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                    keyboardActions = KeyboardActions(onSearch = { search() }),
                )
                if (isSearching) {
                    Text("Buscando…")
                } else if (searchFailed) {
                    Text("Não foi possível pesquisar este livro.")
                } else if (hasSearched && results.isEmpty()) {
                    Text("Nenhum trecho encontrado.")
                } else if (results.isNotEmpty()) {
                    Text("Toque em um trecho para abrir.")
                    LazyColumn(modifier = Modifier.heightIn(max = 320.dp)) {
                        itemsIndexed(results, key = { index, result -> "${result.chunkIndex}:${result.locator?.href}:$index" }) { index, result ->
                            TextButton(
                                onClick = { onOpenResult(result) },
                                modifier = Modifier.fillMaxWidth().testTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-$index"),
                            ) {
                                Text(text = result.label, modifier = Modifier.fillMaxWidth())
                            }
                        }
                    }
                    if (results.size == BookTextSearch.MAX_RESULTS) {
                        Text("Mostrando até ${BookTextSearch.MAX_RESULTS} resultados.")
                    }
                }
            }
        },
        confirmButton = {
            TextButton(
                onClick = ::search,
                enabled = query.isNotBlank() && (!isSearching || query != activeQuery),
            ) {
                Text("Buscar")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Fechar") }
        },
    )
}

internal data class BookTextSearchResult(
    val label: String,
    val chunkIndex: Int? = null,
    val locator: Locator? = null,
)

internal suspend fun PlainTextBookDocument.searchResults(query: String): List<BookTextSearchResult> =
    BookTextSearch.search(chunks, query).map { index ->
        BookTextSearchResult(
            label = "Trecho ${index + 1}: ${BookTextSearch.contextualSnippet(chunks[index], query)}",
            chunkIndex = index,
        )
    }

internal fun Locator.toBookTextSearchResult(index: Int): BookTextSearchResult {
    val snippet = listOfNotNull(text.before, text.highlight, text.after)
        .joinToString(" … ")
        .ifBlank { title ?: href.toString() }
    return BookTextSearchResult("Resultado ${index + 1}: ${snippet.take(140)}", locator = this)
}

internal const val BOOK_TEXT_SEARCH_QUERY_TEST_TAG = "book-text-search-query"
internal const val BOOK_TEXT_SEARCH_RESULT_TEST_TAG = "book-text-search-result"
