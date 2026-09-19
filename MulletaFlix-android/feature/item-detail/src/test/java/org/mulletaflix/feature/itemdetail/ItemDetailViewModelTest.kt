package org.mulletaflix.feature.itemdetail

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.model.*
import org.mulletaflix.domain.repository.*
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import org.mulletaflix.domain.usecase.ManagePlaylistUseCase
import org.mulletaflix.domain.usecase.ToggleFavoriteUseCase
import org.mulletaflix.domain.usecase.TogglePlayedUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class ItemDetailViewModelTest {

    private val dispatcher = StandardTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun createViewModel(
        mediaRepo: MediaRepository,
        authRepo: AuthRepository = FakeAuthRepository(userId = "u1"),
        playbackRepo: PlaybackRepository = FakePlaybackRepository(),
        downloadRepo: DownloadRepository = FakeDownloadRepository(),
        playlistRepo: PlaylistRepository = FakePlaylistRepository(),
    ): ItemDetailViewModel {
        return ItemDetailViewModel(
            getItemDetailUseCase = GetItemDetailUseCase(mediaRepo),
            toggleFavoriteUseCase = ToggleFavoriteUseCase(mediaRepo),
            togglePlayedUseCase = TogglePlayedUseCase(mediaRepo),
            manageDownloadsUseCase = ManageDownloadsUseCase(downloadRepo),
            managePlaylistUseCase = ManagePlaylistUseCase(playlistRepo),
            mediaRepository = mediaRepo,
            authRepository = authRepo,
            playbackRepository = playbackRepo,
        )
    }

    @Test
    fun `loadItem loads movie item details and similar items`() = runTest {
        val movie = MediaItem(id = "m1", name = "Test Movie", type = MediaItemType.Movie)
        val similar = listOf(MediaItem(id = "m2", name = "Similar Movie", type = MediaItemType.Movie))

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(movie)
            override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int): Result<List<MediaItem>> = Result.success(similar)
        }
        val authRepo = FakeAuthRepository(userId = "u1")
        val playbackRepo = FakePlaybackRepository()
        val downloadRepo = FakeDownloadRepository()
        val playlistRepo = FakePlaylistRepository()

        val viewModel = createViewModel(mediaRepo, authRepo, playbackRepo, downloadRepo, playlistRepo)
        advanceUntilIdle()

        viewModel.loadItem("m1")
        advanceUntilIdle()

        val state = viewModel.state.value
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertEquals("Test Movie", state.item?.name)
        assertEquals(1, state.similarItems.size)
        assertEquals("Similar Movie", state.similarItems.first().name)
    }

    @Test
    fun `loadItem for series loads seasons and auto selects first season`() = runTest {
        val series = MediaItem(id = "s1", name = "Test Series", type = MediaItemType.Series)
        val seasons = listOf(
            MediaItem(id = "sea-1", name = "Season 1", type = MediaItemType.Season),
            MediaItem(id = "sea-2", name = "Season 2", type = MediaItemType.Season),
        )
        val episodesS1 = listOf(
            MediaItem(id = "ep-1", name = "Episode 1", type = MediaItemType.Episode),
            MediaItem(id = "ep-2", name = "Episode 2", type = MediaItemType.Episode),
        )

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(series)
            override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(seasons)
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> {
                return if (seasonId == "sea-1") Result.success(episodesS1) else Result.success(emptyList())
            }
        }
        val authRepo = FakeAuthRepository(userId = "u1")
        val viewModel = createViewModel(mediaRepo, authRepo)
        advanceUntilIdle()

        viewModel.loadItem("s1")
        advanceUntilIdle()

        val state = viewModel.state.value
        assertEquals(2, state.seasons.size)
        assertEquals(0, state.selectedSeasonIndex)
        assertEquals(2, state.episodes.size)
        assertEquals("Episode 1", state.episodes.first().name)
    }

    @Test
    fun `selectSeason updates season index and loads episodes for chosen season`() = runTest {
        val series = MediaItem(id = "s1", name = "Test Series", type = MediaItemType.Series)
        val seasons = listOf(
            MediaItem(id = "sea-1", name = "Season 1", type = MediaItemType.Season),
            MediaItem(id = "sea-2", name = "Season 2", type = MediaItemType.Season),
        )
        val episodesS2 = listOf(
            MediaItem(id = "ep-201", name = "S2 Episode 1", type = MediaItemType.Episode),
        )

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(series)
            override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(seasons)
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> {
                return if (seasonId == "sea-2") Result.success(episodesS2) else Result.success(emptyList())
            }
        }
        val authRepo = FakeAuthRepository(userId = "u1")
        val viewModel = createViewModel(mediaRepo, authRepo)
        advanceUntilIdle()

        viewModel.loadItem("s1")
        advanceUntilIdle()

        viewModel.selectSeason(1)
        advanceUntilIdle()

        val state = viewModel.state.value
        assertEquals(1, state.selectedSeasonIndex)
        assertEquals(1, state.episodes.size)
        assertEquals("S2 Episode 1", state.episodes.first().name)
    }

    @Test
    fun `loadItem for a season loads the parent series episodes and selects that season`() = runTest {
        val season2 = MediaItem(
            id = "sea-2", name = "Temporada 2", type = MediaItemType.Season,
            seriesId = "s1", seriesName = "Test Series",
        )
        val seasons = listOf(
            MediaItem(id = "sea-1", name = "Season 1", type = MediaItemType.Season),
            season2,
        )
        val episodesS2 = listOf(MediaItem(id = "ep-201", name = "S2 Episode 1", type = MediaItemType.Episode))
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(season2)
            override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(seasons)
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> =
                if (seasonId == "sea-2") Result.success(episodesS2) else Result.success(emptyList())
        }
        val viewModel = createViewModel(mediaRepo)
        advanceUntilIdle()

        viewModel.loadItem("sea-2")
        advanceUntilIdle()

        val state = viewModel.state.value
        assertEquals(2, state.seasons.size)
        assertEquals(1, state.selectedSeasonIndex)
        assertEquals("S2 Episode 1", state.episodes.firstOrNull()?.name)
    }

    @Test
    fun `loadItem for an episode selects the season that contains it`() = runTest {
        val episode = MediaItem(
            id = "ep-202", name = "S2 Episode 2", type = MediaItemType.Episode,
            seriesId = "s1", seasonId = "sea-2", seasonName = "Temporada 2",
        )
        val seasons = listOf(
            MediaItem(id = "sea-1", name = "Season 1", type = MediaItemType.Season),
            MediaItem(id = "sea-2", name = "Temporada 2", type = MediaItemType.Season),
        )
        var requestedSeasonId: String? = "unset"
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(episode)
            override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(seasons)
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> {
                requestedSeasonId = seasonId
                return Result.success(listOf(episode))
            }
        }
        val viewModel = createViewModel(mediaRepo)
        advanceUntilIdle()

        viewModel.loadItem("ep-202")
        advanceUntilIdle()

        assertEquals("sea-2", requestedSeasonId)
        assertEquals(1, viewModel.state.value.selectedSeasonIndex)
    }

    @Test
    fun `loadItem for a series without seasons still loads all episodes`() = runTest {
        val series = MediaItem(id = "s1", name = "Test Series", type = MediaItemType.Series)
        val allEpisodes = listOf(MediaItem(id = "ep-1", name = "Episode 1", type = MediaItemType.Episode))
        var requestedSeasonId: String? = "unset"
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(series)
            override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(emptyList())
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> {
                requestedSeasonId = seasonId
                return Result.success(allEpisodes)
            }
        }
        val viewModel = createViewModel(mediaRepo)
        advanceUntilIdle()

        viewModel.loadItem("s1")
        advanceUntilIdle()

        assertNull(requestedSeasonId)
        assertTrue(viewModel.state.value.seasons.isEmpty())
        assertEquals(1, viewModel.state.value.episodes.size)
    }

    @Test
    fun `loadItem for a music album loads its tracks through an album parent query`() = runTest {
        val album = MediaItem(id = "al-1", name = "Album", type = MediaItemType.MusicAlbum)
        val tracks = listOf(MediaItem(id = "tr-1", name = "Track", type = MediaItemType.Audio))
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(album)
            override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> {
                lastItemsParentId = parentId
                lastItemsIncludeItemTypes = includeItemTypes
                return Result.success(Pair(tracks, tracks.size))
            }
        }
        val viewModel = createViewModel(mediaRepo)
        advanceUntilIdle()

        viewModel.loadItem("al-1")
        advanceUntilIdle()

        assertEquals("al-1", mediaRepo.lastItemsParentId)
        assertEquals("Audio", mediaRepo.lastItemsIncludeItemTypes)
        assertEquals(1, viewModel.state.value.episodes.size)
    }

    @Test
    fun `toggleFavorite toggles favorite state and calls repository`() = runTest {
        val movie = MediaItem(id = "m1", name = "Movie", type = MediaItemType.Movie, isFavorite = false)
        var markedFavorite = false

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(movie)
            override suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit> {
                markedFavorite = true
                return Result.success(Unit)
            }
        }
        val authRepo = FakeAuthRepository(userId = "u1")
        val viewModel = createViewModel(mediaRepo, authRepo)
        advanceUntilIdle()

        viewModel.loadItem("m1")
        advanceUntilIdle()

        viewModel.toggleFavorite()
        assertTrue(viewModel.state.value.item?.isFavorite == true)

        advanceUntilIdle()
        assertTrue(markedFavorite)
    }

    @Test
    fun `toggleWatched toggles played state and calls repository`() = runTest {
        val movie = MediaItem(id = "m1", name = "Movie", type = MediaItemType.Movie, isPlayed = false)
        var markedPlayed = false

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(movie)
            override suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit> {
                markedPlayed = true
                return Result.success(Unit)
            }
        }
        val authRepo = FakeAuthRepository(userId = "u1")
        val viewModel = createViewModel(mediaRepo, authRepo)
        advanceUntilIdle()

        viewModel.loadItem("m1")
        advanceUntilIdle()

        viewModel.toggleWatched()
        assertTrue(viewModel.state.value.item?.isPlayed == true)

        advanceUntilIdle()
        assertTrue(markedPlayed)
    }

    @Test
    fun `openPlaylistPicker and addToPlaylist updates dialog state and messages`() = runTest {
        val movie = MediaItem(id = "m1", name = "Movie", type = MediaItemType.Movie)
        val playlist = Playlist(id = "p1", name = "My Favorites")
        var addedItemId: String? = null

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(movie)
        }
        val playlistRepo = object : FakePlaylistRepository() {
            override suspend fun getPlaylists(userId: String): Result<List<Playlist>> = Result.success(listOf(playlist))
            override suspend fun addItem(userId: String, playlistId: String, itemId: String): Result<Unit> {
                addedItemId = itemId
                return Result.success(Unit)
            }
        }

        val authRepo = FakeAuthRepository(userId = "u1")
        val viewModel = createViewModel(mediaRepo, authRepo, playlistRepo = playlistRepo)
        advanceUntilIdle()

        viewModel.loadItem("m1")
        advanceUntilIdle()

        viewModel.openPlaylistPicker()
        advanceUntilIdle()

        assertTrue(viewModel.state.value.isPlaylistDialogVisible)
        assertEquals(1, viewModel.state.value.playlists.size)

        viewModel.addToPlaylist(playlist)
        advanceUntilIdle()

        assertEquals("m1", addedItemId)
        assertFalse(viewModel.state.value.isPlaylistDialogVisible)
        assertTrue(viewModel.state.value.playlistMessage?.contains("My Favorites") == true)
    }

    @Test
    fun `loadItem sets error on failure`() = runTest {
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> {
                return Result.failure(IllegalStateException("Network connection failed"))
            }
        }
        val authRepo = FakeAuthRepository(userId = "u1")
        val viewModel = createViewModel(mediaRepo, authRepo)
        advanceUntilIdle()

        viewModel.loadItem("invalid-id")
        advanceUntilIdle()

        val state = viewModel.state.value
        assertFalse(state.isLoading)
        assertNotNull(state.error)
        assertTrue(state.error!!.contains("Network connection failed"))
    }

    @Test
    fun `selectSeason succeeds even if called before init collect finishes`() = runTest {
        val series = MediaItem(id = "s1", name = "Test Series", type = MediaItemType.Series)
        val seasons = listOf(MediaItem(id = "sea-1", name = "Season 1", type = MediaItemType.Season))
        val episodesS1 = listOf(MediaItem(id = "ep-1", name = "Episode 1", type = MediaItemType.Episode))

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.success(series)
            override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(seasons)
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> = Result.success(episodesS1)
        }
        val authRepo = FakeAuthRepository(userId = "u1")
        val viewModel = createViewModel(mediaRepo, authRepo)

        viewModel.loadItem("s1")
        advanceUntilIdle()

        val state = viewModel.state.value
        assertEquals(1, state.seasons.size)
        assertEquals(1, state.episodes.size)
        assertEquals("Episode 1", state.episodes.first().name)
    }

    // ── Fakes ────────────────────────────────────────────────────────────────
    private open class FakeAuthRepository(private val userId: String?) : AuthRepository {
        override suspend fun verifyServer(url: String): Result<ServerVerification> = Result.failure(NotImplementedError())
        override suspend fun register(username: String, password: String): Result<RegistrationResult> = Result.failure(NotImplementedError())
        override suspend fun login(username: String, password: String): Result<UserSession> = Result.failure(NotImplementedError())
        override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(emptyList())
        override suspend fun initiateQuickConnect(): Result<QuickConnectState> = Result.failure(NotImplementedError())
        override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = Result.success(null)
        override suspend fun logout(): Result<Unit> = Result.success(Unit)
        override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.failure(NotImplementedError())
        override fun getSavedServerUrl(): Flow<String> = flowOf("http://localhost:8096")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId(): Flow<String?> = flowOf(userId)
        override fun getSavedUserName(): Flow<String?> = flowOf("User")
        override fun getSavedToken(): Flow<String?> = flowOf("token")
    }

    private open class FakeMediaRepository : MediaRepository {
        var lastItemsParentId: String? = null
        var lastItemsIncludeItemTypes: String? = null
        override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getNextUp(userId: String, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getLibraries(userId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> = Result.success(Pair(emptyList(), 0))
        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.failure(NotImplementedError())
        override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getSpecialFeatures(userId: String, itemId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun search(userId: String, searchTerm: String, limit: Int, includeItemTypes: String?): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getLiveTvChannels(userId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getRecordings(userId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getSuggestions(userId: String, itemId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override fun observeFavorites(userId: String): Flow<List<MediaItem>> = emptyFlow()
        override fun observeRecentlyWatched(userId: String): Flow<List<MediaItem>> = emptyFlow()
    }

    private open class FakePlaybackRepository : PlaybackRepository {
        override suspend fun getPlaybackInfo(
            itemId: String,
            userId: String,
            audioStreamIndex: Int?,
            subtitleStreamIndex: Int?,
            startTimeTicks: Long?,
        ): Result<PlaybackInfo> = Result.failure(NotImplementedError())

        override suspend fun reportPlaybackStart(
            itemId: String,
            playSessionId: String?,
            mediaSourceId: String?,
            audioIndex: Int?,
            subtitleIndex: Int?,
            positionTicks: Long,
        ): Result<Unit> = Result.success(Unit)

        override suspend fun reportPlaybackProgress(
            itemId: String,
            playSessionId: String?,
            mediaSourceId: String?,
            audioIndex: Int?,
            subtitleIndex: Int?,
            positionTicks: Long,
            isPaused: Boolean,
        ): Result<Unit> = Result.success(Unit)

        override suspend fun reportPlaybackStopped(
            itemId: String,
            playSessionId: String?,
            mediaSourceId: String?,
            positionTicks: Long,
        ): Result<Unit> = Result.success(Unit)
    }

    private open class FakeDownloadRepository : DownloadRepository {
        override fun enqueue(id: String, title: String, uri: String): Result<Unit> = Result.success(Unit)
        override fun retry(id: String, title: String, uri: String): Result<Unit> = Result.success(Unit)
        override fun remove(id: String): Result<Unit> = Result.success(Unit)
        override fun pauseAll(): Result<Unit> = Result.success(Unit)
        override fun resumeAll(): Result<Unit> = Result.success(Unit)
        override fun observeDownloads(): Flow<List<DownloadEntry>> = emptyFlow()
    }

    private open class FakePlaylistRepository : PlaylistRepository {
        override suspend fun getPlaylists(userId: String): Result<List<Playlist>> = Result.success(emptyList())
        override suspend fun createPlaylist(userId: String, name: String, itemId: String?): Result<Playlist> = Result.success(Playlist("p1", name))
        override suspend fun addItem(userId: String, playlistId: String, itemId: String): Result<Unit> = Result.success(Unit)
    }
}

