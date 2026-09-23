package org.mulletaflix.feature.search

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.SearchHistoryRepository
import org.mulletaflix.domain.usecase.SearchMediaUseCase
import javax.inject.Inject

data class SearchState(
    val query: String = "",
    val activeFilter: SearchFilter? = null,
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val results: List<MediaItem> = emptyList(),
    val history: List<String> = emptyList(),
    val error: String? = null,
    val isOffline: Boolean = false,
    /**
     * Quantos itens o servidor diz que casam com a busca, não quantos vieram.
     *
     * Nulo quando o servidor não informou (ou quando nenhuma busca respondeu ainda):
     * nesse caso a tela não afirma nada sobre truncamento, porque não sabe.
     */
    val totalMatching: Int? = null,
    /**
     * Uma página seguinte está a caminho.
     *
     * Separado de [isLoading] de propósito: [isLoading] troca a lista inteira por um
     * indicador, e "carregar mais" precisa **manter** o que já está na tela enquanto a
     * próxima página chega.
     */
    val isLoadingMore: Boolean = false,
    /**
     * O servidor contou mais itens do que já chegaram, e ainda não desistimos de buscar.
     *
     * Diferente de [isTruncated]: um "carregar mais" que falhou mantém `hasMore` em
     * `false` para o botão não virar uma armadilha de tentar-e-falhar sem fim.
     */
    val hasMore: Boolean = false,
) {
    /**
     * A busca pede uma página por vez e o servidor conta quantos itens casam. Quando ele
     * conta mais do que já chegou, a tela precisa oferecer o resto — mostrar 30 de 412
     * sem avisar faz o usuário concluir que a biblioteca tem 30 resultados.
     */
    val isTruncated: Boolean
        get() = totalMatching != null && totalMatching > results.size
}

@HiltViewModel
class SearchViewModel @Inject constructor(
    private val searchMediaUseCase: SearchMediaUseCase,
    private val authRepository: AuthRepository,
    private val searchHistoryRepository: SearchHistoryRepository,
    private val networkMonitor: NetworkMonitor,
) : ViewModel() {

    private val _state = MutableStateFlow(SearchState())
    val state: StateFlow<SearchState> = _state.asStateFlow()

    private var searchJob: Job? = null
    private var historyJob: Job? = null
    private var currentUserId: String? = null
    private var searchGeneration = 0L
    private var historyGeneration = 0L

    init {
        viewModelScope.launch {
            var previousOnline: Boolean? = null
            networkMonitor.isOnline.distinctUntilChanged().collect { online ->
                val recovered = previousOnline == false && online
                previousOnline = online
                _state.update { it.copy(isOffline = !online) }
                if (recovered) refreshSearch()
            }
        }
        viewModelScope.launch {
            authRepository.getSavedUserId().distinctUntilChanged().collect { userId ->
                val userChanged = currentUserId != userId
                currentUserId = userId
                if (userChanged) {
                    searchJob?.cancel()
                    ++searchGeneration
                    _state.update {
                        it.copy(
                            results = emptyList(),
                            totalMatching = null,
                            hasMore = false,
                            isLoadingMore = false,
                            isLoading = false,
                            isRefreshing = false,
                            error = null,
                        )
                    }
                }
                val generation = ++historyGeneration
                historyJob?.cancel()
                historyJob = launch {
                    searchHistoryRepository.observeHistory(userId).collect { history ->
                        if (generation == historyGeneration && currentUserId == userId) {
                            _state.update { it.copy(history = history) }
                        }
                    }
                }
            }
        }
    }

    fun onQueryChange(newQuery: String) {
        // Uma pergunta nova invalida a contagem antiga: o total que estava na tela
        // respondia à busca anterior, e mantê-lo faria a linha "Mostrando 30 de 412"
        // falar de uma busca que já não é a do campo. A lista fica (evita a tela
        // piscar a cada tecla); só a contagem some até a resposta chegar.
        _state.update {
            it.copy(
                query = newQuery,
                error = null,
                totalMatching = null,
                // Uma pergunta nova cancela a paginação da anterior. Sem baixar
                // `isLoadingMore` aqui, a resposta da página antiga seria descartada pela
                // geração e a flag ficaria presa em `true` — botão girando para sempre.
                hasMore = false,
                isLoadingMore = false,
            )
        }
        searchJob?.cancel()
        val generation = ++searchGeneration
        if (newQuery.isBlank()) {
            // Clearing the box invalidates the in-flight search, and that
            // search's own terminal branches skip their clear once the
            // generation moved on. Both flags have to be lowered here, or a
            // cancelled pull-to-refresh (`refreshSearch`) leaves its indicator
            // spinning with no request behind it.
            _state.update { it.copy(results = emptyList(), totalMatching = null, hasMore = false, isLoadingMore = false, isLoading = false, isRefreshing = false, error = null) }
            return
        }

        searchJob = viewModelScope.launch {
            delay(350) // debounce
            performSearch(newQuery, _state.value.activeFilter, generation)
        }
    }

    fun search(query: String) {
        val normalizedQuery = normalizeSearchQuery(query) ?: return
        _state.update {
            it.copy(
                query = normalizedQuery,
                error = null,
                totalMatching = null,
                hasMore = false,
                isLoadingMore = false,
            )
        }
        // An explicit search is remembered whatever it returns: the viewer asked for it.
        rememberSearch(normalizedQuery, currentUserId)
        searchJob?.cancel()
        val generation = ++searchGeneration
        searchJob = viewModelScope.launch {
            performSearch(normalizedQuery, _state.value.activeFilter, generation)
        }
    }

    /**
     * Puts [query] at the top of the recent list and stores it.
     *
     * Shared by the explicit search and the debounced one, so both paths keep the same
     * limit, the same de-duplication and the same store.
     */
    private fun rememberSearch(query: String, userId: String?) {
        _state.update { it.copy(history = (listOf(query) + it.history).distinct().take(10)) }
        viewModelScope.launch { searchHistoryRepository.add(userId, query) }
    }

    fun retrySearch() {
        val currentQuery = _state.value.query
        if (currentQuery.isNotBlank()) {
            searchJob?.cancel()
            val generation = ++searchGeneration
            searchJob = viewModelScope.launch {
                performSearch(currentQuery, _state.value.activeFilter, generation, isRefresh = false)
            }
        }
    }

    fun refreshSearch() {
        val currentQuery = _state.value.query
        if (currentQuery.isBlank() || _state.value.isLoading || _state.value.isRefreshing) return
        searchJob?.cancel()
        val generation = ++searchGeneration
        searchJob = viewModelScope.launch {
            performSearch(currentQuery, _state.value.activeFilter, generation, isRefresh = true)
        }
    }

    fun setFilter(filter: SearchFilter?) {
        // Trocar o filtro troca a pergunta: o total da busca anterior não conta mais
        // nada sobre "só filmes" ou "só músicas".
        _state.update {
            it.copy(
                activeFilter = filter,
                error = null,
                totalMatching = null,
                hasMore = false,
                isLoadingMore = false,
            )
        }
        val currentQuery = _state.value.query
        if (currentQuery.isNotBlank()) {
            searchJob?.cancel()
            val generation = ++searchGeneration
            searchJob = viewModelScope.launch {
                performSearch(currentQuery, filter, generation, isRefresh = false)
            }
        }
    }

    fun removeHistoryItem(term: String) {
        _state.update { it.copy(history = it.history.filterNot { item -> item == term }) }
        viewModelScope.launch { searchHistoryRepository.remove(currentUserId, term) }
    }

    fun clearHistory() {
        _state.update { it.copy(history = emptyList()) }
        viewModelScope.launch { searchHistoryRepository.clear(currentUserId) }
    }

    private suspend fun performSearch(
        query: String,
        filter: SearchFilter?,
        generation: Long,
        isRefresh: Boolean = false,
    ) {
        val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: run {
            if (generation == searchGeneration &&
                _state.value.query == query &&
                _state.value.activeFilter == filter &&
                currentUserId == null
            ) {
                _state.update { it.copy(isLoading = false, isRefreshing = false, error = "Usuário não autenticado") }
            }
            return
        }
        if (!isCurrentSearch(query, filter, generation, userId)) return
        if (_state.value.isOffline) {
            // Nada foi perguntado ao servidor, então a lista — e a contagem — que
            // estão na tela respondem à busca **anterior**. Mantê-las faria o aviso
            // "Mostrando 30 de 412" afirmar um número para uma busca que nunca rodou.
            // Um refresh é diferente: ali a intenção é justamente ver a mesma lista
            // de novo, e o cartão de erro já explica que não deu.
            _state.update {
                it.copy(
                    results = if (isRefresh) it.results else emptyList(),
                    totalMatching = if (isRefresh) it.totalMatching else null,
                    // Offline não há próxima página: `loadMore` sai na primeira linha.
                    hasMore = if (isRefresh) it.hasMore else false,
                    isLoadingMore = false,
                    isLoading = false,
                    isRefreshing = false,
                    error = "Você está offline. A busca será retomada quando a conexão voltar.",
                )
            }
            return
        }
        _state.update {
            it.copy(
                isLoading = !isRefresh,
                isRefreshing = isRefresh,
                isLoadingMore = false,
                error = null,
            )
        }

        val typeParam = filter.toApiItemType()

        searchMediaUseCase(
            userId = userId,
            query = query,
            itemTypes = typeParam,
        ).onSuccess { results ->
            if (isCurrentSearch(query, filter, generation, userId)) {
                _state.update {
                    it.copy(
                        results = results.items,
                        totalMatching = results.totalMatching,
                        hasMore = results.isTruncated,
                        isLoading = false,
                        isRefreshing = false,
                        error = null,
                    )
                }
                // A search that found something is a search worth offering again, and the
                // debounced path is how almost every search happens — the box is typed
                // into, not submitted. Only `search()` (Enter, a history tap, voice) used
                // to write history, so "Suas buscas recentes aparecerão aqui." stayed
                // empty for a viewer who never pressed Enter.
                //
                // A search that found nothing is deliberately not remembered: it is a
                // typo or a title the server does not have, and re-running it is useless.
                if (results.items.isNotEmpty()) rememberSearch(query, userId)
            }
        }.onFailure {
            if (isCurrentSearch(query, filter, generation, userId)) {
                _state.update {
                    it.copy(
                        results = if (isRefresh) it.results else emptyList(),
                        totalMatching = if (isRefresh) it.totalMatching else null,
                        // Uma busca que falhou não oferece "carregar mais": a próxima
                        // página sairia da mesma busca quebrada.
                        hasMore = false,
                        isLoading = false,
                        isRefreshing = false,
                        error = "Erro ao buscar conteúdo",
                    )
                }
            }
        }
    }

    /**
     * Pede a página seguinte da **mesma** busca.
     *
     * A lista é anexada, não trocada: o que já foi lido continua na tela com a posição
     * de rolagem. Três cuidados que a paginação da Biblioteca já tinha aprendido:
     *
     *  - o `startIndex` é o **tamanho do que já chegou**, não `página × tamanho`: o
     *    servidor pode devolver uma página menor que o pedido, e avançar pelo tamanho
     *    pedido pularia itens;
     *  - a página nova é **deduplicada por id**, porque uma busca com ordenação instável
     *    pode repetir um item entre páginas;
     *  - se a página voltar vazia, `hasMore` cai: insistir buscaria o mesmo vazio.
     */
    fun loadMore() {
        val current = _state.value
        if (!current.hasMore || current.isLoadingMore || current.isLoading || current.isRefreshing) return
        val query = current.query
        val filter = current.activeFilter
        if (query.isBlank() || current.isOffline) return
        val userId = currentUserId ?: return
        val startIndex = current.results.size
        val generation = searchGeneration

        // A flag sobe **antes** do lançamento: se subisse depois, uma resposta rápida
        // chegaria com `isLoadingMore` ainda falso e o botão continuaria clicável.
        _state.update { it.copy(isLoadingMore = true, error = null) }

        viewModelScope.launch {
            val typeParam = filter.toApiItemType()
            searchMediaUseCase(
                userId = userId,
                query = query,
                itemTypes = typeParam,
                startIndex = startIndex,
            ).onSuccess { page ->
                if (!isCurrentSearch(query, filter, generation, userId)) return@onSuccess
                _state.update { state ->
                    val known = state.results.mapTo(mutableSetOf()) { it.id }
                    val appended = page.items.filter { known.add(it.id) }
                    val shown = state.results.size + appended.size
                    val reportedTotal = page.totalMatching
                    state.copy(
                        results = state.results + appended,
                        totalMatching = reportedTotal ?: state.totalMatching,
                        // Nada novo significa que insistir devolveria o mesmo: parar. E
                        // quando o servidor contou, quem decide é a contagem.
                        hasMore = appended.isNotEmpty() &&
                            (reportedTotal == null || reportedTotal > shown),
                        isLoadingMore = false,
                    )
                }
            }.onFailure {
                if (isCurrentSearch(query, filter, generation, userId)) {
                    _state.update {
                        it.copy(
                            isLoadingMore = false,
                            // Uma página que falhou não deixa o botão tentando para
                            // sempre: o usuário pede de novo quando quiser.
                            hasMore = false,
                            error = "Não foi possível carregar mais resultados.",
                        )
                    }
                }
            }
        }
    }

    private fun SearchFilter?.toApiItemType(): String? = when (this) {
        SearchFilter.Movies -> "Movie"
        SearchFilter.Series -> "Series"
        SearchFilter.Episodes -> "Episode"
        SearchFilter.Music -> "Audio"
        SearchFilter.Albums -> "MusicAlbum"
        SearchFilter.Artists -> "MusicArtist"
        SearchFilter.People -> "Person"
        null -> null
    }

    private fun isCurrentSearch(
        query: String,
        filter: SearchFilter?,
        generation: Long,
        userId: String,
    ): Boolean =
        generation == searchGeneration &&
            _state.value.query == query &&
            _state.value.activeFilter == filter &&
            currentUserId == userId
}
