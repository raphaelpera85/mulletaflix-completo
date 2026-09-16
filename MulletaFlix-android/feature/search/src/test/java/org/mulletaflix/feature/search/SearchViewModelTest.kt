package org.mulletaflix.feature.search

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.SearchHintItem
import org.mulletaflix.domain.repository.SearchRepository
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.SearchMediaUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class SearchViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var searchRepository: RecordingSearchRepository
    private lateinit var viewModel: SearchViewModel

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
        searchRepository = RecordingSearchRepository()
        viewModel = SearchViewModel(SearchMediaUseCase(searchRepository), FakeAuthRepository())
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
        repeat(12) { viewModel.search("term-$it") }
        advanceUntilIdle()

        assertEquals(10, viewModel.state.value.history.size)
        assertEquals("term-11", viewModel.state.value.history.first())
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
    fun `removeHistoryItem removes single entry from search history`() = runTest {
        viewModel.search("batman")
        viewModel.search("superman")
        advanceUntilIdle()

        assertEquals(listOf("superman", "batman"), viewModel.state.value.history)

        viewModel.removeHistoryItem("superman")
        assertEquals(listOf("batman"), viewModel.state.value.history)
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

    private class FakeAuthRepository : AuthRepository {
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
