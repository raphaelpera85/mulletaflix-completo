package org.mulletaflix.feature.search

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.channelFlow
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.withContext
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.SearchHintItem
import org.mulletaflix.domain.repository.SearchHistoryRepository
import org.mulletaflix.domain.repository.SearchRepository
import org.mulletaflix.domain.repository.SearchResults
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.SearchMediaUseCase
import org.mulletaflix.core.common.network.NetworkMonitor

@OptIn(ExperimentalCoroutinesApi::class)
class SearchViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var searchRepository: RecordingSearchRepository
    private lateinit var viewModel: SearchViewModel
    private lateinit var historyRepository: FakeSearchHistoryRepository
    private lateinit var networkState: MutableStateFlow<Boolean>

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
        searchRepository = RecordingSearchRepository()
        historyRepository = FakeSearchHistoryRepository()
        networkState = MutableStateFlow(true)
        viewModel = createViewModel()
    }

    @After
    fun tearDown() = Dispatchers.resetMain()

    @Test
    fun `debounced search sends selected media type`() = runTest {
        viewModel.setFilter(SearchFilter.Movies)
        viewModel.onQueryChange("matrix")
        advanceTimeBy(349)
        assertFalse(searchRepository.called)

        advanceTimeBy(1)
        advanceUntilIdle()

        assertEquals("matrix", searchRepository.term)
        assertEquals("Movie", searchRepository.itemTypes)
    }

    @Test
    fun `type ahead loads deduplicated filtered hints before the full search`() = runTest {
        searchRepository.hints = listOf(
            SearchHintItem("movie-1", "Matrix", "Movie", 1999, null),
            SearchHintItem("movie-1", "Matrix", "Movie", 1999, null),
            SearchHintItem("series-1", "Matrix Files", "Series", 2024, null),
        )
        viewModel.setFilter(SearchFilter.Movies)
        viewModel.onQueryChange("mat")

        advanceTimeBy(179)
        assertTrue(viewModel.state.value.hints.isEmpty())
        advanceTimeBy(1)
        runCurrent()
        assertEquals(listOf("movie-1"), viewModel.state.value.hints.map { it.id })

        advanceUntilIdle()

        assertTrue(viewModel.state.value.hints.isEmpty())
        assertEquals("mat", searchRepository.hintTerm)
        assertTrue(searchRepository.called)
    }

    @Test
    fun `manual search keeps ten distinct history entries`() = runTest {
        advanceUntilIdle()
        repeat(12) { viewModel.search("term-$it") }
        advanceUntilIdle()

        assertEquals(10, viewModel.state.value.history.size)
        assertEquals("term-11", viewModel.state.value.history.first())
        assertEquals(10, historyRepository.entries.size)
    }

    @Test
    fun `manual search trims the query in state and history`() = runTest {
        advanceUntilIdle()
        viewModel.search("  matrix  ")
        advanceUntilIdle()

        assertEquals("matrix", viewModel.state.value.query)
        assertEquals(listOf("matrix"), viewModel.state.value.history)
        assertEquals(listOf("matrix"), historyRepository.entries)
        assertEquals("matrix", searchRepository.term)
    }

    @Test
    fun `blank manual search does not create history or request the server`() = runTest {
        viewModel.search("   ")
        advanceUntilIdle()

        assertFalse(searchRepository.called)
        assertTrue(historyRepository.entries.isEmpty())
    }

    @Test
    fun `search failure sets error message and clears loading`() = runTest {
        searchRepository.shouldFail = true
        viewModel.search("matrix")
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isLoading)
        assertEquals("Erro ao buscar conteúdo", viewModel.state.value.error)
        assertEquals(0, viewModel.state.value.results.size)
    }

    @Test
    fun `retrySearch executes search again after failure`() = runTest {
        searchRepository.shouldFail = true
        viewModel.search("matrix")
        advanceUntilIdle()
        assertEquals("Erro ao buscar conteúdo", viewModel.state.value.error)

        searchRepository.shouldFail = false
        viewModel.retrySearch()
        advanceUntilIdle()

        assertEquals(null, viewModel.state.value.error)
        assertEquals(1, viewModel.state.value.results.size)
    }

    @Test
    fun `successful search carries the server total into state`() = runTest {
        searchRepository.totalMatching = 412
        viewModel.search("matrix")
        advanceUntilIdle()

        assertEquals(412, viewModel.state.value.totalMatching)
        assertTrue(viewModel.state.value.isTruncated)
    }

    @Test
    fun `a total equal to what arrived is not truncation`() = runTest {
        searchRepository.totalMatching = 1
        viewModel.search("matrix")
        advanceUntilIdle()

        assertEquals(1, viewModel.state.value.totalMatching)
        assertFalse(viewModel.state.value.isTruncated)
    }

    @Test
    fun `search without a server total does not claim truncation`() = runTest {
        searchRepository.totalMatching = null
        viewModel.search("matrix")
        advanceUntilIdle()

        assertEquals(null, viewModel.state.value.totalMatching)
        assertFalse(viewModel.state.value.isTruncated)
    }

    @Test
    fun `clearing the query drops a stale server total`() = runTest {
        searchRepository.totalMatching = 412
        viewModel.search("matrix")
        advanceUntilIdle()
        assertEquals(412, viewModel.state.value.totalMatching)

        viewModel.onQueryChange("")
        advanceUntilIdle()

        // Sem isso a tela continuaria dizendo "Mostrando 0 de 412" depois de o campo
        // ser esvaziado, ao lado do histórico de buscas.
        assertEquals(null, viewModel.state.value.totalMatching)
    }

    @Test
    fun `typing a new query drops the previous server total before the answer arrives`() = runTest {
        searchRepository.totalMatching = 412
        viewModel.search("batman")
        advanceUntilIdle()
        assertEquals(412, viewModel.state.value.totalMatching)

        // Uma pergunta nova: o 412 respondia a "batman" e não diz nada sobre "zzz".
        // A lista antiga fica de propósito (evita piscar), mas a contagem não pode.
        viewModel.onQueryChange("zzz")

        assertEquals(null, viewModel.state.value.totalMatching)
        assertEquals("zzz", viewModel.state.value.query)
    }

    @Test
    fun `offline with a new query drops the previous results and total`() = runTest {
        searchRepository.totalMatching = 412
        viewModel.search("batman")
        advanceUntilIdle()
        assertEquals(1, viewModel.state.value.results.size)

        networkState.value = false
        advanceUntilIdle()

        viewModel.search("zzz")
        advanceUntilIdle()

        // Nada foi perguntado ao servidor: a tela não pode mostrar 30 cartões de
        // "batman" com "Mostrando 30 de 412" embaixo de "zzz".
        assertTrue(viewModel.state.value.results.isEmpty())
        assertEquals(null, viewModel.state.value.totalMatching)
        assertEquals(
            "Você está offline. A busca será retomada quando a conexão voltar.",
            viewModel.state.value.error,
        )
    }

    @Test
    fun `an offline refresh keeps the list and the total it is refreshing`() = runTest {
        searchRepository.totalMatching = 412
        viewModel.search("batman")
        advanceUntilIdle()

        networkState.value = false
        advanceUntilIdle()

        // Puxar para atualizar é o caso oposto: a intenção é rever a mesma lista, e o
        // cartão de erro já explica que a atualização não aconteceu.
        viewModel.refreshSearch()
        advanceUntilIdle()

        assertEquals(1, viewModel.state.value.results.size)
        assertEquals(412, viewModel.state.value.totalMatching)
    }

    @Test
    fun `changing the filter drops the previous server total`() = runTest {
        searchRepository.totalMatching = 412
        viewModel.search("batman")
        advanceUntilIdle()
        assertEquals(412, viewModel.state.value.totalMatching)

        viewModel.setFilter(SearchFilter.Movies)

        assertEquals(null, viewModel.state.value.totalMatching)
    }

    @Test
    fun `refreshSearch keeps the current query and completes refresh state`() = runTest {
        val controlledRepository = ControlledSearchRepository()
        viewModel = SearchViewModel(
            SearchMediaUseCase(controlledRepository),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("matrix")
        runCurrent()
        controlledRepository.complete("matrix", "Initial result")
        advanceUntilIdle()

        viewModel.refreshSearch()
        runCurrent()
        assertTrue(viewModel.state.value.isRefreshing)
        controlledRepository.complete("matrix", "Refreshed result")
        advanceUntilIdle()

        assertEquals("Refreshed result", viewModel.state.value.results.single().name)
        assertFalse(viewModel.state.value.isRefreshing)
        assertFalse(viewModel.state.value.isLoading)
    }

    @Test
    fun `refresh failure preserves current results for retry`() = runTest {
        val controlledRepository = ControlledSearchRepository()
        viewModel = SearchViewModel(
            SearchMediaUseCase(controlledRepository),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("matrix")
        runCurrent()
        controlledRepository.complete("matrix", "Initial result")
        advanceUntilIdle()

        controlledRepository.failNext = true
        viewModel.refreshSearch()
        advanceUntilIdle()

        assertEquals("Initial result", viewModel.state.value.results.single().name)
        assertEquals("Erro ao buscar conteúdo", viewModel.state.value.error)
        assertFalse(viewModel.state.value.isRefreshing)
    }

    @Test
    fun `late result from an older query cannot replace the latest result`() = runTest {
        val controlledRepository = ControlledSearchRepository()
        viewModel = SearchViewModel(
            SearchMediaUseCase(controlledRepository),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("old")
        runCurrent()
        viewModel.search("new")
        runCurrent()

        controlledRepository.complete("new", "New result")
        advanceUntilIdle()
        assertEquals("New result", viewModel.state.value.results.single().name)

        controlledRepository.complete("old", "Old result")
        advanceUntilIdle()
        assertEquals("New result", viewModel.state.value.results.single().name)
    }

    @Test
    fun `a search typed into the box is remembered`() = runTest {
        // O caminho normal de uma busca é digitar, não apertar Enter. Só `search()`
        // gravava histórico, então "Suas buscas recentes aparecerão aqui." continuava
        // vazio para quem nunca apertou Enter — mesmo tendo buscado.
        viewModel.onQueryChange("batman")
        advanceTimeBy(400)
        advanceUntilIdle()

        assertTrue("a busca precisa ter ido ao servidor", searchRepository.called)
        assertEquals(listOf("batman"), viewModel.state.value.history)
        assertEquals(listOf("batman"), historyRepository.entries)
    }

    @Test
    fun `a search that found nothing is not remembered`() = runTest {
        // Um termo sem resultado é um erro de digitação ou um título que o servidor não
        // tem; guardá-lo só polui a lista de buscas recentes.
        searchRepository.shouldFail = false
        searchRepository.emptyResults = true
        viewModel.onQueryChange("batmna")
        advanceTimeBy(400)
        advanceUntilIdle()

        assertTrue("a busca precisa ter acontecido", searchRepository.called)
        assertEquals(emptyList<String>(), viewModel.state.value.history)
        assertEquals(emptyList<String>(), historyRepository.entries)
    }

    @Test
    fun `removeHistoryItem removes single entry from search history`() = runTest {
        advanceUntilIdle()
        viewModel.search("batman")
        viewModel.search("superman")
        advanceUntilIdle()

        assertEquals(listOf("superman", "batman"), viewModel.state.value.history)

        viewModel.removeHistoryItem("superman")
        assertEquals(listOf("batman"), viewModel.state.value.history)
        advanceUntilIdle()
        assertEquals(listOf("batman"), historyRepository.entries)
    }

    @Test
    fun `history loaded for one user does not leak to another user`() = runTest {
        historyRepository.seed("user-1", listOf("batman"))
        viewModel = SearchViewModel(
            SearchMediaUseCase(searchRepository),
            SwitchingAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        assertEquals(listOf("batman"), viewModel.state.value.history)
    }

    @Test
    fun `late history emission from a previous user cannot replace current history`() = runTest {
        val auth = SwitchingAuthRepository()
        val oldHistory = CompletableDeferred<List<String>>()
        val repository = FakeSearchHistoryRepository().apply {
            seed("user-1", listOf("old"))
            seed("user-2", listOf("current"))
            lateUserOneHistory = oldHistory
        }
        viewModel = SearchViewModel(
            SearchMediaUseCase(searchRepository),
            auth,
            repository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        auth.switchTo("user-2")
        runCurrent()
        assertEquals(listOf("current"), viewModel.state.value.history)

        oldHistory.complete(listOf("stale"))
        advanceUntilIdle()

        assertEquals(listOf("current"), viewModel.state.value.history)
    }

    @Test
    fun `late search result from a previous user cannot replace current session`() = runTest {
        val auth = SwitchingAuthRepository()
        val controlledRepository = ControlledSearchRepository()
        viewModel = SearchViewModel(
            SearchMediaUseCase(controlledRepository),
            auth,
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("matrix")
        runCurrent()
        assertEquals("user-1", controlledRepository.userIdFor("matrix"))

        auth.switchTo("user-2")
        runCurrent()
        assertTrue(viewModel.state.value.results.isEmpty())

        controlledRepository.complete("matrix", "Stale result")
        advanceUntilIdle()

        assertTrue(viewModel.state.value.results.isEmpty())
        assertFalse(viewModel.state.value.isLoading)
    }

    @Test
    fun `search is refreshed once when connectivity returns`() = runTest {
        advanceUntilIdle()
        viewModel.search("matrix")
        advanceUntilIdle()
        assertEquals(1, searchRepository.calls)

        networkState.value = false
        advanceUntilIdle()
        assertTrue(viewModel.state.value.isOffline)

        networkState.value = true
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isOffline)
        assertEquals(2, searchRepository.calls)
    }

    @Test
    fun `offline search does not call the server and resumes after reconnect`() = runTest {
        advanceUntilIdle()
        networkState.value = false
        advanceUntilIdle()

        viewModel.search("matrix")
        advanceUntilIdle()

        assertEquals(0, searchRepository.calls)
        assertEquals(
            "Você está offline. A busca será retomada quando a conexão voltar.",
            viewModel.state.value.error,
        )

        networkState.value = true
        advanceUntilIdle()

        assertEquals(1, searchRepository.calls)
        assertEquals(null, viewModel.state.value.error)
    }

    /** Servidor de mentira que pagina de verdade, com total fixo. */
    private class PagedSearchRepository(
        private val total: Int,
        private val pageSize: Int = 30,
        /** Itens que a página seguinte repete, para exercitar a deduplicação. */
        private val repeatFromPreviousPage: Int = 0,
    ) : SearchRepository {
        val starts = mutableListOf<Int>()
        var failNext = false

        override suspend fun searchHints(term: String, userId: String?) =
            Result.success(emptyList<SearchHintItem>())

        override suspend fun searchItems(
            term: String,
            userId: String,
            itemTypes: String?,
            startIndex: Int,
        ): Result<SearchResults> {
            starts += startIndex
            if (failNext) {
                failNext = false
                return Result.failure(Exception("HTTP 503"))
            }
            val end = minOf(startIndex + pageSize, total)
            val items = (startIndex until end).map {
                MediaItem("id-$it", "Item $it", MediaItemType.Movie)
            }
            val overlap = if (startIndex == 0) emptyList() else {
                (maxOf(0, startIndex - repeatFromPreviousPage) until startIndex).map {
                    MediaItem("id-$it", "Item $it", MediaItemType.Movie)
                }
            }
            return Result.success(SearchResults(overlap + items, total))
        }
    }

    @Test
    fun `loadMore appends the next page without dropping what is on screen`() = runTest {
        val paged = PagedSearchRepository(total = 100)
        viewModel = SearchViewModel(
            SearchMediaUseCase(paged),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("a")
        advanceUntilIdle()
        assertEquals(30, viewModel.state.value.results.size)
        assertTrue(viewModel.state.value.hasMore)

        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals("a página nova é anexada, não troca a lista", 60, viewModel.state.value.results.size)
        assertEquals("o início da segunda página é o tamanho do que já chegou", listOf(0, 30), paged.starts)
        assertEquals("id-0", viewModel.state.value.results.first().id)
        assertTrue(viewModel.state.value.hasMore)
        assertFalse(viewModel.state.value.isLoadingMore)
    }

    @Test
    fun `loadMore stops offering more once everything arrived`() = runTest {
        val paged = PagedSearchRepository(total = 45)
        viewModel = SearchViewModel(
            SearchMediaUseCase(paged),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("a")
        advanceUntilIdle()
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(45, viewModel.state.value.results.size)
        assertFalse("chegou tudo: o botão sai de cena", viewModel.state.value.hasMore)
        assertFalse(viewModel.state.value.isTruncated)
    }

    @Test
    fun `loadMore does not repeat an item the server sent twice`() = runTest {
        val paged = PagedSearchRepository(total = 60, repeatFromPreviousPage = 2)
        viewModel = SearchViewModel(
            SearchMediaUseCase(paged),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("a")
        advanceUntilIdle()
        viewModel.loadMore()
        advanceUntilIdle()

        val ids = viewModel.state.value.results.map { it.id }
        assertEquals("a lista não pode ter id repetido", ids.distinct(), ids)
        assertEquals(60, ids.size)
    }

    @Test
    fun `a failed page says so and stops offering more`() = runTest {
        val paged = PagedSearchRepository(total = 100)
        viewModel = SearchViewModel(
            SearchMediaUseCase(paged),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("a")
        advanceUntilIdle()
        paged.failNext = true

        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals("o que já estava na tela continua", 30, viewModel.state.value.results.size)
        assertEquals("Não foi possível carregar mais resultados.", viewModel.state.value.error)
        assertFalse("o botão não pode virar armadilha de tentar-e-falar", viewModel.state.value.hasMore)
        assertFalse(viewModel.state.value.isLoadingMore)
    }

    @Test
    fun `a new query drops the pagination of the previous one`() = runTest {
        val paged = PagedSearchRepository(total = 100)
        viewModel = SearchViewModel(
            SearchMediaUseCase(paged),
            FakeAuthRepository(),
            historyRepository,
            FakeNetworkMonitor(networkState),
        )
        advanceUntilIdle()

        viewModel.search("a")
        advanceUntilIdle()
        viewModel.loadMore()
        advanceUntilIdle()
        assertTrue(viewModel.state.value.hasMore)

        // Uma pergunta nova: a paginação da anterior morre **na hora**, antes do debounce.
        // Se `isLoadingMore` não baixasse aqui, a resposta da página antiga seria
        // descartada pela geração e o indicador giraria para sempre.
        viewModel.onQueryChange("b")
        assertFalse(viewModel.state.value.hasMore)
        assertFalse(viewModel.state.value.isLoadingMore)

        advanceUntilIdle()

        // E a busca nova recomeça da primeira página, em vez de continuar de onde a
        // anterior parou.
        assertEquals(listOf(0, 30, 0), paged.starts)
        assertEquals(30, viewModel.state.value.results.size)
    }

    private fun createViewModel(): SearchViewModel = SearchViewModel(
        SearchMediaUseCase(searchRepository),
        FakeAuthRepository(),
        historyRepository,
        FakeNetworkMonitor(networkState),
    )

    private class RecordingSearchRepository : SearchRepository {
        var called = false
        var term: String? = null
        var itemTypes: String? = null
        var calls = 0
        var shouldFail = false
        var hintTerm: String? = null
        var hints: List<SearchHintItem> = emptyList()

        /** Devolve uma busca bem-sucedida sem resultado. */
        var emptyResults = false

        /** Total que o servidor informa; nulo = o servidor não contou. */
        var totalMatching: Int? = null

        override suspend fun searchHints(term: String, userId: String?): Result<List<SearchHintItem>> {
            hintTerm = term
            return Result.success(hints)
        }
        override suspend fun searchItems(
            term: String,
            userId: String,
            itemTypes: String?,
            startIndex: Int,
        ): Result<SearchResults> {
            called = true
            calls++
            this.term = term
            this.itemTypes = itemTypes
            if (shouldFail) {
                return Result.failure(Exception("Network error"))
            }
            if (emptyResults) return Result.success(SearchResults(emptyList(), totalMatching))
            return Result.success(
                SearchResults(
                    items = listOf(MediaItem("1", "Result", MediaItemType.Movie)),
                    totalMatching = totalMatching,
                ),
            )
        }
    }

    private class ControlledSearchRepository : SearchRepository {
        private val pending = mutableMapOf<String, CompletableDeferred<Result<SearchResults>>>()
        private val users = mutableMapOf<String, String>()
        var failNext = false

        override suspend fun searchHints(term: String, userId: String?) = Result.success(emptyList<SearchHintItem>())

        override suspend fun searchItems(
            term: String,
            userId: String,
            itemTypes: String?,
            startIndex: Int,
        ): Result<SearchResults> {
            users[term] = userId
            if (failNext) {
                failNext = false
                return Result.failure(Exception("Network error"))
            }
            return withContext(NonCancellable) {
                val deferred = pending.getOrPut(term) { CompletableDeferred() }
                try {
                    deferred.await()
                } finally {
                    pending.remove(term, deferred)
                }
            }
        }

        fun userIdFor(term: String): String? = users[term]

        fun complete(term: String, title: String) {
            pending.getOrPut(term) { CompletableDeferred() }
                .complete(Result.success(SearchResults(listOf(MediaItem(term, title, MediaItemType.Movie)))))
        }
    }

    private class FakeSearchHistoryRepository : SearchHistoryRepository {
        val entries = mutableListOf<String>()
        private val byUser = mutableMapOf<String?, MutableList<String>>()
        var lateUserOneHistory: CompletableDeferred<List<String>>? = null

        override fun observeHistory(userId: String?): Flow<List<String>> {
            val deferred = lateUserOneHistory
            if (userId == "user-1" && deferred != null) {
                return channelFlow {
                    send(byUser[userId]?.toList().orEmpty())
                    withContext(NonCancellable) {
                        send(deferred.await())
                    }
                }
            }
            return MutableStateFlow(byUser[userId]?.toList().orEmpty())
        }

        override suspend fun add(userId: String?, query: String) {
            val list = byUser.getOrPut(userId) { mutableListOf() }
            list.remove(query)
            list.add(0, query)
            while (list.size > 10) list.removeAt(list.lastIndex)
            entries.clear(); entries.addAll(list)
        }

        override suspend fun remove(userId: String?, query: String) {
            byUser[userId]?.remove(query)
            entries.clear(); entries.addAll(byUser[userId].orEmpty())
        }

        override suspend fun clear(userId: String?) { byUser.remove(userId); entries.clear() }

        fun seed(userId: String?, values: List<String>) { byUser[userId] = values.toMutableList() }
    }

    private class SwitchingAuthRepository : FakeAuthRepository() {
        private val userId = MutableStateFlow<String?>("user-1")

        override fun getSavedUserId(): Flow<String?> = userId

        fun switchTo(nextUserId: String) {
            userId.value = nextUserId
        }
    }

    private class FakeNetworkMonitor(
        private val online: Flow<Boolean>,
    ) : NetworkMonitor {
        override val isOnline: Flow<Boolean> = online
    }

    private open class FakeAuthRepository : AuthRepository {
        override fun getSavedUserId(): Flow<String?> = MutableStateFlow("user-1")
        override fun getSavedToken(): Flow<String?> = MutableStateFlow("token")
        override fun getSavedServerUrl(): Flow<String> = MutableStateFlow("http://localhost")
        override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Test", "1"))
        override suspend fun register(username: String, password: String) = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String) = Result.success(UserSession("1", username, "t", null))
        override suspend fun getAvailableUsers() = Result.success(emptyList<org.mulletaflix.domain.repository.AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.success(QuickConnectState("1234", "secret", false))
        override suspend fun checkQuickConnect(secret: String) = Result.success(null)
        override suspend fun logout() = Result.success(Unit)
        override suspend fun setServerUrl(url: String) = Unit
    }
}
