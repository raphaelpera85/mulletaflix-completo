package org.mulletaflix.feature.home

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.SavedServer
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.GetHomeFeedUseCase

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class HomeViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before fun setUp() = Dispatchers.setMain(dispatcher)
    @After fun tearDown() = Dispatchers.resetMain()

    @Test fun `session expiry stops home loading without requesting content`() = runTest {
        val repository = FakeMediaRepository()
        val useCase = GetHomeFeedUseCase(repository)
        val viewModel = HomeViewModel(useCase, FakeSessionRepository(userId = null), FakeNetworkMonitor(), FakeAuthRepository())
        advanceUntilIdle()

        assertEquals(0, repository.requestCount)
        assertTrue(viewModel.state.value.error?.contains("Sessão expirada") == true)
        assertTrue(!viewModel.state.value.isLoading)
    }

    @Test fun `partial section failure such as live tv does not crash home screen`() = runTest {
        val movie = MediaItem("m1", "Movie 1", org.mulletaflix.domain.model.MediaItemType.Movie)
        val lib = MediaItem("lib1", "Filmes", org.mulletaflix.domain.model.MediaItemType.CollectionFolder)
        val repository = object : FakeMediaRepository() {
            override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(listOf(movie))
            override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
            override suspend fun getLibraries(userId: String) = Result.success(listOf(lib))
            override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(listOf(movie))
            override suspend fun getLiveTvChannels(userId: String) = Result.failure<List<MediaItem>>(Exception("Live TV disabled on server"))
        }
        val useCase = GetHomeFeedUseCase(repository)
        val profile = UserProfile(id = "u1", name = "Raphael", primaryImageTag = "avatar-tag")
        val viewModel = HomeViewModel(useCase, FakeSessionRepository(userId = "u1"), FakeNetworkMonitor(), FakeAuthRepository(profile))
        advanceUntilIdle()

        assertEquals(null, viewModel.state.value.error)
        assertEquals(false, viewModel.state.value.isLoading)
        assertEquals(1, viewModel.state.value.resumeItems.size)
        assertEquals(1, viewModel.state.value.libraries.size)
        assertEquals(0, viewModel.state.value.liveTvChannels.size)
        assertEquals(profile, viewModel.state.value.userProfile)
    }

    @Test fun `network monitor transitions update isOffline state`() = runTest {
        val networkMonitor = FakeNetworkMonitor(initialOnline = true)
        val repository = FakeMediaRepository()
        val useCase = GetHomeFeedUseCase(repository)
        val viewModel = HomeViewModel(useCase, FakeSessionRepository(userId = null), networkMonitor, FakeAuthRepository())
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isOffline)

        networkMonitor.setOnline(false)
        advanceUntilIdle()

        assertTrue(viewModel.state.value.isOffline)
    }

    private class FakeNetworkMonitor(initialOnline: Boolean = true) : NetworkMonitor {
        private val _isOnline = MutableStateFlow(initialOnline)
        override val isOnline: Flow<Boolean> = _isOnline
        fun setOnline(online: Boolean) { _isOnline.value = online }
    }

    private class FakeSessionRepository(private val userId: String?) : SessionRepository {
        override fun getAccessToken() = flowOf(null)
        override fun getDeviceId() = flowOf("home-test")
        override fun getBaseUrl() = flowOf("http://localhost:8096")
        override fun getCurrentUserId() = flowOf(userId)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private class FakeAuthRepository(
        private val profile: UserProfile? = null,
    ) : AuthRepository {
        override suspend fun verifyServer(url: String): Result<ServerVerification> = Result.success(ServerVerification("Test", "1"))
        override suspend fun register(username: String, password: String): Result<RegistrationResult> = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String): Result<UserSession> = Result.success(UserSession("u1", username, "token", null))
        override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(emptyList())
        override suspend fun initiateQuickConnect(): Result<QuickConnectState> = Result.success(QuickConnectState("123456", "secret", false))
        override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = Result.success(null)
        override suspend fun logout(): Result<Unit> = Result.success(Unit)
        override suspend fun getCurrentUserProfile(): Result<UserProfile> = profile?.let { Result.success(it) } ?: Result.failure(UnsupportedOperationException())
        override fun getSavedServerUrl() = flowOf("http://localhost:8096")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId() = flowOf<String?>("u1")
        override fun getSavedToken() = flowOf<String?>("token")
        override fun getSavedServers() = flowOf<List<SavedServer>>(emptyList())
    }

    private open class FakeMediaRepository : MediaRepository {
        var requestCount = 0
        private fun unavailable(): Nothing { requestCount++; error("not expected") }
        override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> = unavailable()
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int): Result<List<MediaItem>> = unavailable()
        override suspend fun getNextUp(userId: String, limit: Int): Result<List<MediaItem>> = unavailable()
        override suspend fun getLibraries(userId: String): Result<List<MediaItem>> = unavailable()
        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> = unavailable()
        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = unavailable()
        override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int): Result<List<MediaItem>> = unavailable()
        override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = unavailable()
        override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> = unavailable()
        override suspend fun getSpecialFeatures(userId: String, itemId: String): Result<List<MediaItem>> = unavailable()
        override suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit> = unavailable()
        override suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit> = unavailable()
        override suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit> = unavailable()
        override suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit> = unavailable()
        override suspend fun search(userId: String, searchTerm: String, limit: Int, includeItemTypes: String?): Result<List<MediaItem>> = unavailable()
        override suspend fun getLiveTvChannels(userId: String): Result<List<MediaItem>> = unavailable()
        override suspend fun getRecordings(userId: String): Result<List<MediaItem>> = unavailable()
        override suspend fun getSuggestions(userId: String, itemId: String): Result<List<MediaItem>> = unavailable()
        override fun observeFavorites(userId: String): Flow<List<MediaItem>> = emptyFlow()
        override fun observeRecentlyWatched(userId: String): Flow<List<MediaItem>> = emptyFlow()
    }
}
