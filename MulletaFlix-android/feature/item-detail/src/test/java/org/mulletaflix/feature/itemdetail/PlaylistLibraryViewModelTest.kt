package org.mulletaflix.feature.itemdetail

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
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
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.repository.*
import org.mulletaflix.domain.usecase.ManagePlaylistUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class PlaylistLibraryViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before fun setUp() { Dispatchers.setMain(dispatcher) }
    @After fun tearDown() { Dispatchers.resetMain() }

    @Test
    fun loadsPlaylistsItemsAndNextPageWithoutDuplicateMedia() = runTest {
        val playlist = Playlist("p1", "Favoritos")
        val first = MediaItem("m1", "Filme 1", MediaItemType.Movie)
        val duplicate = MediaItem("m1", "Filme repetido", MediaItemType.Movie)
        val second = MediaItem("m2", "Filme 2", MediaItemType.Movie)
        val repo = object : PlaylistRepository {
            override suspend fun getPlaylists(userId: String) = Result.success(listOf(playlist))
            override suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int) =
                if (startIndex == 0) Result.success(listOf(first) to 51)
                else Result.success(listOf(duplicate, second) to 51)
            override suspend fun createPlaylist(userId: String, name: String, itemId: String?) = Result.success(playlist)
            override suspend fun addItem(userId: String, playlistId: String, itemId: String) = Result.success(Unit)
        }
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(repo), fakeAuthRepository())
        advanceUntilIdle()

        assertEquals(listOf(playlist), viewModel.state.value.playlists)
        assertEquals(listOf(first), viewModel.state.value.items)
        assertEquals(51, viewModel.state.value.totalItems)

        viewModel.loadNextPage()
        advanceUntilIdle()

        assertEquals(listOf(first, second), viewModel.state.value.items)
        assertFalse(viewModel.state.value.isLoadingItems)
    }

    @Test
    fun advancesPlaylistOffsetByReceivedItemsEvenWhenVisibleItemsAreDeduplicated() = runTest {
        val playlist = Playlist("p1", "Favoritos")
        val first = MediaItem("m1", "Filme 1", MediaItemType.Movie)
        val duplicate = MediaItem("m1", "Filme 1 duplicado", MediaItemType.Movie)
        val second = MediaItem("m2", "Filme 2", MediaItemType.Movie)
        val third = MediaItem("m3", "Filme 3", MediaItemType.Movie)
        val starts = mutableListOf<Int>()
        val repo = object : PlaylistRepository {
            override suspend fun getPlaylists(userId: String) = Result.success(listOf(playlist))
            override suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int): Result<Pair<List<MediaItem>, Int>> {
                starts += startIndex
                return if (startIndex == 0) Result.success(listOf(first, duplicate) to 4)
                else Result.success(listOf(second, third) to 4)
            }
            override suspend fun createPlaylist(userId: String, name: String, itemId: String?) = Result.success(playlist)
            override suspend fun addItem(userId: String, playlistId: String, itemId: String) = Result.success(Unit)
        }
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(repo), fakeAuthRepository())
        advanceUntilIdle()

        viewModel.loadNextPage()
        advanceUntilIdle()

        assertEquals(listOf(0, 2), starts)
        assertEquals(listOf(first, second, third), viewModel.state.value.items)
        assertFalse(viewModel.state.value.hasMoreItems)
    }

    @Test
    fun failedPlaylistPageKeepsItemsAndRetryRequestsSameOffset() = runTest {
        val playlist = Playlist("p1", "Favoritos")
        val first = MediaItem("m1", "Filme 1", MediaItemType.Movie)
        val second = MediaItem("m2", "Filme 2", MediaItemType.Movie)
        val starts = mutableListOf<Int>()
        var failNextPage = true
        val repo = object : PlaylistRepository {
            override suspend fun getPlaylists(userId: String) = Result.success(listOf(playlist))
            override suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int): Result<Pair<List<MediaItem>, Int>> {
                starts += startIndex
                if (startIndex == 0) return Result.success(listOf(first) to 2)
                if (failNextPage) {
                    failNextPage = false
                    return Result.failure(IllegalStateException("HTTP 503"))
                }
                return Result.success(listOf(second) to 2)
            }
            override suspend fun createPlaylist(userId: String, name: String, itemId: String?) = Result.success(playlist)
            override suspend fun addItem(userId: String, playlistId: String, itemId: String) = Result.success(Unit)
        }
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(repo), fakeAuthRepository())
        advanceUntilIdle()

        viewModel.loadNextPage()
        advanceUntilIdle()
        assertEquals(listOf(first), viewModel.state.value.items)
        assertEquals("HTTP 503", viewModel.state.value.itemsError)

        viewModel.retryItems()
        advanceUntilIdle()

        assertEquals(listOf(0, 1, 1), starts)
        assertEquals(listOf(first, second), viewModel.state.value.items)
        assertEquals(null, viewModel.state.value.itemsError)
    }

    private fun fakeAuthRepository() = object : AuthRepository {
        override suspend fun verifyServer(url: String) = Result.failure<ServerVerification>(UnsupportedOperationException())
        override suspend fun register(username: String, password: String) = Result.failure<RegistrationResult>(UnsupportedOperationException())
        override suspend fun login(username: String, password: String) = Result.failure<UserSession>(UnsupportedOperationException())
        override suspend fun getAvailableUsers() = Result.success(emptyList<AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.failure<QuickConnectState>(UnsupportedOperationException())
        override suspend fun checkQuickConnect(secret: String) = Result.success<UserSession?>(null)
        override suspend fun logout() = Result.success(Unit)
        override fun getSavedServerUrl() = flowOf("http://localhost")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId() = flowOf("u1")
        override fun getSavedToken() = flowOf("token")
    }
}
