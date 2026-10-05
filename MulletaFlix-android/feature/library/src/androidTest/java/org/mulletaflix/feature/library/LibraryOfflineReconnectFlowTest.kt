package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.data.repository.LibraryCatalogCacheRepositoryImpl
import org.mulletaflix.domain.model.LibraryFilterOptions
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetLibraryItemsUseCase
import java.util.UUID

class LibraryOfflineReconnectFlowTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun persistedLibraryRestoresOfflineAndRefreshesWhenConnectivityReturns() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val suffix = UUID.randomUUID().toString().replace("-", "")
        val userId = "offline-user-$suffix"
        val libraryId = "offline-library-$suffix"
        val mediaId = "movie-$suffix"
        val session = TestSessionRepository(userId, suffix)
        val media = TestMediaRepository(libraryId, mediaId)
        val auth = TestAuthRepository(userId)
        val settings = TestSettingsRepository()
        val onlineNetwork = MutableTestNetworkMonitor(online = true)
        val firstCache = LibraryCatalogCacheRepositoryImpl(context, session)
        val firstViewModel = newViewModel(media, auth, settings, onlineNetwork, firstCache)
        val selectedScreen = mutableStateOf(0 to firstViewModel)
        var openedItemId: String? = null

        composeRule.setContent {
            MaterialTheme {
                key(selectedScreen.value.first) {
                    LibraryScreen(
                        libraryId = libraryId,
                        onItemClick = { openedItemId = it },
                        onBack = {},
                        viewModel = selectedScreen.value.second,
                    )
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 10_000) {
            composeRule.onAllNodesWithText("Catálogo online").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.waitUntil(timeoutMillis = 10_000) {
            runBlocking(Dispatchers.IO) { firstCache.read(userId, libraryId) != null }
        }
        val saved = withContext(Dispatchers.IO) { firstCache.read(userId, libraryId) }
        assertNotNull("The online screen must persist the catalog through the real DataStore cache", saved)
        assertEquals("Catálogo online", saved?.items?.single()?.name)

        media.catalog = listOf(movie(mediaId, "Catálogo atualizado"))
        val offlineNetwork = MutableTestNetworkMonitor(online = false)
        // A new ViewModel and cache repository recreate the process-owned graph;
        // the actual Android process is intentionally not killed by this test.
        val recreatedViewModel = newViewModel(
            media,
            auth,
            settings,
            offlineNetwork,
            LibraryCatalogCacheRepositoryImpl(context, session),
        )
        composeRule.runOnIdle { selectedScreen.value = 1 to recreatedViewModel }

        composeRule.waitUntil(timeoutMillis = 10_000) {
            composeRule.onAllNodesWithText("Sem conexão.", substring = true).fetchSemanticsNodes().isNotEmpty() &&
                composeRule.onAllNodesWithText("Catálogo online").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Sem conexão.", substring = true).assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Catálogo online", substring = true).performClick()
        composeRule.onNodeWithText(
            "Prévia do catálogo salvo neste dispositivo. Os detalhes completos e a reprodução por streaming precisam de conexão.",
        ).assertIsDisplayed()
        offlineNetwork.setOnline(true)
        composeRule.waitUntil(timeoutMillis = 10_000) {
            composeRule.onAllNodesWithText("Catálogo atualizado").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Catálogo atualizado").assertIsDisplayed()

        // The still-open preview follows the restored connectivity state.
        composeRule.onNodeWithText("Abrir detalhes").assertIsDisplayed().performClick()
        composeRule.runOnIdle { assertEquals(mediaId, openedItemId) }
    }

    private fun newViewModel(
        media: MediaRepository,
        auth: AuthRepository,
        settings: SettingsRepository,
        network: NetworkMonitor,
        cache: LibraryCatalogCacheRepositoryImpl,
    ) = LibraryViewModel(
        getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
        getItemDetailUseCase = GetItemDetailUseCase(media),
        authRepository = auth,
        settingsRepository = settings,
        networkMonitor = network,
        libraryCatalogCache = cache,
    )

    private fun movie(id: String, title: String) = MediaItem(
        id = id,
        name = title,
        type = MediaItemType.Movie,
        overview = "Sinopse local disponível.",
        year = 2025,
        officialRating = "12",
        genres = listOf("Drama"),
    )

    private class TestMediaRepository(libraryId: String, mediaId: String) : MediaRepository {
        @Volatile var catalog: List<MediaItem> = listOf(
            MediaItem(mediaId, "Catálogo online", MediaItemType.Movie),
        )

        override suspend fun getItems(
            userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?,
            filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?,
            officialRatings: String?, isPlayed: Boolean?, isFavorite: Boolean?,
        ): Result<Pair<List<MediaItem>, Int>> = Result.success(catalog to catalog.size)

        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(
            if (itemId.startsWith("offline-library-")) {
                MediaItem(itemId, "Filmes", MediaItemType.CollectionFolder, collectionType = "movies", isFolder = true)
            } else {
                catalog.firstOrNull { it.id == itemId } ?: MediaItem(itemId, "Mídia", MediaItemType.Movie)
            },
        )

        override suspend fun getLibraryFilterOptions(userId: String, parentId: String, includeItemTypes: String?) =
            Result.success(LibraryFilterOptions())

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
        override suspend fun getLiveTvChannelPreview(userId: String) = Result.success(emptyList<MediaItem>())
    }

    private class MutableTestNetworkMonitor(online: Boolean) : NetworkMonitor {
        private val state = MutableStateFlow(online)
        override val isOnline: Flow<Boolean> = state
        fun setOnline(online: Boolean) { state.value = online }
    }

    private class TestSessionRepository(userId: String, serverId: String) : SessionRepository {
        private val scope = HomeFeedCacheScope(serverId, "https://cache-$serverId.example", userId)
        override fun getAccessToken() = flowOf<String?>("test-only-token")
        override fun getDeviceId() = flowOf("test-device")
        override fun getBaseUrl() = flowOf(scope.serverUrl)
        override fun getCurrentUserId() = flowOf<String?>(scope.userId)
        override fun getHomeFeedCacheScope() = flowOf(scope)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private class TestAuthRepository(private val userId: String) : AuthRepository {
        override fun getSavedUserId() = flowOf<String?>(userId)
        override fun getSavedToken() = flowOf<String?>("test-only-token")
        override fun getSavedServerUrl() = flowOf("https://test.example")
        override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Test", "1"))
        override suspend fun register(username: String, password: String) = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String) = Result.success(UserSession(userId, username, "test-only-token", null))
        override suspend fun getAvailableUsers() = Result.success(emptyList<org.mulletaflix.domain.repository.AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.success(QuickConnectState("123456", "test", false))
        override suspend fun checkQuickConnect(secret: String) = Result.success<UserSession?>(null)
        override suspend fun logout() = Result.success(Unit)
        override suspend fun setServerUrl(url: String) = Unit
    }

    private class TestSettingsRepository : SettingsRepository {
        override fun getTheme() = flowOf(AppThemeSetting.Dark)
        override suspend fun setTheme(theme: AppThemeSetting) = Unit
        override fun isPiPEnabled() = flowOf(true)
        override suspend fun setPiPEnabled(enabled: Boolean) = Unit
        override fun getPreferredAudioLanguage() = flowOf<String?>(null)
        override suspend fun setPreferredAudioLanguage(language: String?) = Unit
        override fun getPreferredSubtitleLanguage() = flowOf<String?>(null)
        override suspend fun setPreferredSubtitleLanguage(language: String?) = Unit
        override fun isAutoPlayEnabled() = flowOf(true)
        override suspend fun setAutoPlayEnabled(enabled: Boolean) = Unit
        override fun isSkipIntroEnabled() = flowOf(true)
        override suspend fun setSkipIntroEnabled(enabled: Boolean) = Unit
        override fun getDefaultQuality() = flowOf("Auto")
        override suspend fun setDefaultQuality(quality: String) = Unit
        override fun getDefaultPlaybackSpeed() = flowOf(1f)
        override suspend fun setDefaultPlaybackSpeed(speed: Float) = Unit
        override suspend fun clearLocalPreferences() = Unit
        override fun getSubtitleFontSize() = flowOf(100)
        override suspend fun setSubtitleFontSize(size: Int) = Unit
        override fun getDefaultAspectRatio() = flowOf("FIT")
        override suspend fun setDefaultAspectRatio(aspectRatio: String) = Unit
        override fun isLibraryGridViewEnabled() = flowOf(true)
        override suspend fun setLibraryGridViewEnabled(enabled: Boolean) = Unit
        override fun getDefaultLibrarySort() = flowOf("SortName")
        override suspend fun setDefaultLibrarySort(sortBy: String) = Unit
        override fun getDefaultLibraryFilters() = flowOf(emptySet<String>())
        override suspend fun setDefaultLibraryFilters(filters: Set<String>) = Unit
    }
}
