package org.mulletaflix.feature.library

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.GetFavoriteItemsUseCase

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class FavoritesViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var media: FakeMediaRepository

    @Before fun setUp() {
        Dispatchers.setMain(dispatcher)
        media = FakeMediaRepository()
    }

    @After fun tearDown() = Dispatchers.resetMain()

    private fun createViewModel() = FavoritesViewModel(
        getFavoriteItemsUseCase = GetFavoriteItemsUseCase(media),
        authRepository = FakeAuthRepository(),
    )

    @Test
    fun `loads favorites with server filter and supports pagination`() = runTest {
        val first = MediaItem("one", "One", MediaItemType.Movie)
        val second = MediaItem("two", "Two", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        media.pages[1] = Result.success(listOf(second) to 2)
        val viewModel = createViewModel()

        advanceUntilIdle()
        assertEquals(listOf(first), viewModel.state.value.items)
        assertEquals("IsFavorite", media.lastFilters)
        assertEquals(true, media.lastIsFavorite)
        assertTrue(viewModel.state.value.hasMore)

        viewModel.loadMore()
        advanceUntilIdle()
        assertEquals(listOf(first, second), viewModel.state.value.items)
        assertEquals(1, media.lastStartIndex)
        assertTrue(!viewModel.state.value.hasMore)
    }

    @Test
    fun `pagination failure preserves loaded favorites and exposes retryable error`() = runTest {
        val first = MediaItem("one", "One", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        media.pages[1] = Result.failure(IllegalStateException("network"))
        val viewModel = createViewModel()

        advanceUntilIdle()
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(listOf(first), viewModel.state.value.items)
        assertTrue(viewModel.state.value.error?.contains("network") == true)
        assertTrue(viewModel.state.value.hasMore)
    }

    private class FakeMediaRepository : MediaRepository {
        val pages = mutableMapOf<Int, Result<Pair<List<MediaItem>, Int>>>()
        var lastFilters: String? = null
        var lastIsFavorite: Boolean? = null
        var lastStartIndex = -1

        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> {
            lastFilters = filters
            lastIsFavorite = isFavorite
            lastStartIndex = startIndex
            return pages[startIndex] ?: Result.success(emptyList<MediaItem>() to 0)
        }
        override suspend fun getItem(userId: String, itemId: String) = Result.success(MediaItem(itemId, itemId, MediaItemType.Movie))
        override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getSeasons(userId: String, seriesId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?) = Result.success(emptyList<MediaItem>())
        override suspend fun getSpecialFeatures(userId: String, itemId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun markAsPlayed(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun markAsUnplayed(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun markAsFavorite(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun unmarkAsFavorite(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun search(userId: String, searchTerm: String, limit: Int, includeItemTypes: String?) = Result.success(emptyList<MediaItem>())
        override suspend fun getLiveTvChannels(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getRecordings(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getSuggestions(userId: String, itemId: String) = Result.success(emptyList<MediaItem>())
        override fun observeFavorites(userId: String): Flow<List<MediaItem>> = MutableStateFlow(emptyList())
        override fun observeRecentlyWatched(userId: String): Flow<List<MediaItem>> = MutableStateFlow(emptyList())
    }

    private class FakeAuthRepository : AuthRepository {
        override fun getSavedUserId() = MutableStateFlow<String?>("user-1")
        override fun getSavedToken() = MutableStateFlow<String?>("token")
        override fun getSavedServerUrl() = MutableStateFlow("http://localhost")
        override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Test", "1"))
        override suspend fun register(username: String, password: String) = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String) = Result.success(UserSession("user-1", username, "token", null))
        override suspend fun getAvailableUsers() = Result.success(emptyList<org.mulletaflix.domain.repository.AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.success(QuickConnectState("123456", "secret", false))
        override suspend fun checkQuickConnect(secret: String) = Result.success(null)
        override suspend fun logout() = Result.success(Unit)
        override suspend fun setServerUrl(url: String) = Unit
    }
}
