package org.mulletaflix.feature.home

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import kotlinx.coroutines.withContext
import kotlinx.coroutines.NonCancellable
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

    @Test fun `home feed becomes usable before a slow profile response`() = runTest {
        val profileStarted = CompletableDeferred<Unit>()
        val releaseProfile = CompletableDeferred<Unit>()
        val movie = MediaItem("m1", "Movie 1", org.mulletaflix.domain.model.MediaItemType.Movie)
        val repository = object : FakeMediaRepository() {
            override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(listOf(movie))
            override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
            override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
            override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
            override suspend fun getLiveTvChannels(userId: String) = Result.success(emptyList<MediaItem>())
        }
        val profile = UserProfile(id = "u1", name = "Raphael")
        val authRepository = object : FakeAuthRepository(profile) {
            override suspend fun getCurrentUserProfile(): Result<UserProfile> {
                profileStarted.complete(Unit)
                releaseProfile.await()
                return Result.success(profile)
            }
        }

        val viewModel = HomeViewModel(
            GetHomeFeedUseCase(repository),
            FakeSessionRepository(userId = "u1"),
            FakeNetworkMonitor(),
            authRepository,
        )
        runCurrent()
        profileStarted.await()

        assertFalse(viewModel.state.value.isLoading)
        assertEquals(listOf(movie), viewModel.state.value.resumeItems)
        assertEquals(null, viewModel.state.value.userProfile)

        releaseProfile.complete(Unit)
        advanceUntilIdle()
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

    @Test fun `refreshIfIdle does not cancel an active TV refresh`() = runTest {
        val responseRelease = CompletableDeferred<Unit>()
        var resumeCalls = 0
        val repository = object : FakeMediaRepository() {
            override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> {
                resumeCalls++
                responseRelease.await()
                return Result.success(emptyList())
            }

            override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
            override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
            override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
            override suspend fun getLiveTvChannels(userId: String) = Result.success(emptyList<MediaItem>())
        }
        val viewModel = HomeViewModel(
            GetHomeFeedUseCase(repository),
            FakeSessionRepository(userId = "u1"),
            FakeNetworkMonitor(),
            FakeAuthRepository(),
        )
        runCurrent()

        viewModel.refreshIfIdle()

        assertEquals(1, resumeCalls)
        responseRelease.complete(Unit)
        advanceUntilIdle()
    }

    @Test fun `a late refresh cannot overwrite a newer home response`() = runTest {
        val firstResponse = CompletableDeferred<Unit>()
        val repository = object : FakeMediaRepository() {
            var resumeCalls = 0

            override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> {
                resumeCalls++
                if (resumeCalls == 1) {
                    withContext(NonCancellable) { firstResponse.await() }
                    return Result.success(listOf(MediaItem("old", "Resposta antiga", org.mulletaflix.domain.model.MediaItemType.Movie)))
                }
                return Result.success(listOf(MediaItem("new", "Resposta nova", org.mulletaflix.domain.model.MediaItemType.Movie)))
            }

            override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
            override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
            override suspend fun getLiveTvChannels(userId: String) = Result.success(emptyList<MediaItem>())
            override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?) = Result.success(emptyList<MediaItem>() to 0)
        }
        val viewModel = HomeViewModel(
            GetHomeFeedUseCase(repository),
            FakeSessionRepository(userId = "u1"),
            FakeNetworkMonitor(),
            FakeAuthRepository(),
        )
        runCurrent()

        viewModel.refresh()
        runCurrent()
        assertEquals("Resposta nova", viewModel.state.value.resumeItems.single().name)

        firstResponse.complete(Unit)
        advanceUntilIdle()
        assertEquals("Resposta nova", viewModel.state.value.resumeItems.single().name)
    }

    @Test fun `home reloads for the new user and ignores a late previous session`() = runTest {
        val oldResponse = CompletableDeferred<Unit>()
        val old = MediaItem("old", "Conta antiga", org.mulletaflix.domain.model.MediaItemType.Movie)
        val fresh = MediaItem("fresh", "Conta atual", org.mulletaflix.domain.model.MediaItemType.Movie)
        val repository = object : FakeMediaRepository() {
            override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> {
                if (userId == "u1") {
                    withContext(NonCancellable) { oldResponse.await() }
                    return Result.success(listOf(old))
                }
                return Result.success(listOf(fresh))
            }

            override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
            override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
            override suspend fun getLiveTvChannels(userId: String) = Result.success(emptyList<MediaItem>())
        }
        val session = FakeSessionRepository("u1")
        val viewModel = HomeViewModel(
            GetHomeFeedUseCase(repository),
            session,
            FakeNetworkMonitor(),
            FakeAuthRepository(),
        )
        runCurrent()

        session.userIdState.value = "u2"
        runCurrent()
        advanceUntilIdle()
        assertEquals(listOf(fresh), viewModel.state.value.resumeItems)

        oldResponse.complete(Unit)
        advanceUntilIdle()
        assertEquals(listOf(fresh), viewModel.state.value.resumeItems)
    }

    private class FakeNetworkMonitor(initialOnline: Boolean = true) : NetworkMonitor {
        private val _isOnline = MutableStateFlow(initialOnline)
        override val isOnline: Flow<Boolean> = _isOnline
        fun setOnline(online: Boolean) { _isOnline.value = online }
    }

    private class FakeSessionRepository(private val userId: String?) : SessionRepository {
        val userIdState = MutableStateFlow(userId)
        override fun getAccessToken() = flowOf(null)
        override fun getDeviceId() = flowOf("home-test")
        override fun getBaseUrl() = flowOf("http://localhost:8096")
        override fun getCurrentUserId() = userIdState
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private open class FakeAuthRepository(
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
