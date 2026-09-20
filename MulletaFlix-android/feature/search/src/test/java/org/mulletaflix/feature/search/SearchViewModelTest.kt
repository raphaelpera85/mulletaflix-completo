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
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.SearchMediaUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class SearchViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var searchRepository: RecordingSearchRepository
    private lateinit var viewModel: SearchViewModel
    private lateinit var historyRepository: FakeSearchHistoryRepository

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
        searchRepository = RecordingSearchRepository()
        historyRepository = FakeSearchHistoryRepository()
        viewModel = SearchViewModel(SearchMediaUseCase(searchRepository), FakeAuthRepository(), historyRepository)
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
    fun `refreshSearch keeps the current query and completes refresh state`() = runTest {
        val controlledRepository = ControlledSearchRepository()
        viewModel = SearchViewModel(
            SearchMediaUseCase(controlledRepository),
            FakeAuthRepository(),
            historyRepository,
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
        viewModel = SearchViewModel(SearchMediaUseCase(searchRepository), SwitchingAuthRepository(), historyRepository)
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
        viewModel = SearchViewModel(SearchMediaUseCase(searchRepository), auth, repository)
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

    private class RecordingSearchRepository : SearchRepository {
        var called = false
        var term: String? = null
        var itemTypes: String? = null
        var shouldFail = false
        override suspend fun searchHints(term: String, userId: String?) = Result.success(emptyList<SearchHintItem>())
        override suspend fun searchItems(term: String, userId: String, itemTypes: String?): Result<List<MediaItem>> {
            called = true
            this.term = term
            this.itemTypes = itemTypes
            if (shouldFail) {
                return Result.failure(Exception("Network error"))
            }
            return Result.success(listOf(MediaItem("1", "Result", MediaItemType.Movie)))
        }
    }

    private class ControlledSearchRepository : SearchRepository {
        private val pending = mutableMapOf<String, CompletableDeferred<Result<List<MediaItem>>>>()
        private val users = mutableMapOf<String, String>()
        var failNext = false

        override suspend fun searchHints(term: String, userId: String?) = Result.success(emptyList<SearchHintItem>())

        override suspend fun searchItems(term: String, userId: String, itemTypes: String?): Result<List<MediaItem>> {
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
                .complete(Result.success(listOf(MediaItem(term, title, MediaItemType.Movie))))
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
