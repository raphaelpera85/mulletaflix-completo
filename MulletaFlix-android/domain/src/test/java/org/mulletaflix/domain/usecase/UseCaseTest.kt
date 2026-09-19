package org.mulletaflix.domain.usecase

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.*
import org.mulletaflix.domain.repository.*

class UseCaseTest {

    @Test
    fun `GetUserProfileUseCase returns user profile on success`() = runTest {
        val expected = UserProfile(
            id = "u1",
            name = "Test User",
            isAdministrator = true,
            canDownload = true,
            canAccessLiveTv = true,
            canPlayMedia = true,
        )
        val authRepo = object : FakeAuthRepository() {
            override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.success(expected)
        }
        val useCase = GetUserProfileUseCase(authRepo)
        val result = useCase()

        assertTrue(result.isSuccess)
        assertEquals(expected, result.getOrNull())
    }

    @Test
    fun `LogoutUseCase triggers auth logout`() = runTest {
        var loggedOut = false
        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> {
                loggedOut = true
                return Result.success(Unit)
            }
        }
        val useCase = LogoutUseCase(authRepo)
        val result = useCase()

        assertTrue(result.isSuccess)
        assertTrue(loggedOut)
    }

    @Test
    fun `GetItemDetailUseCase retrieves item by id`() = runTest {
        val expected = MediaItem(
            id = "m1",
            name = "Inception",
            type = MediaItemType.Movie,
        )
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> {
                return if (userId == "u1" && itemId == "m1") Result.success(expected) else Result.failure(Exception("Not found"))
            }
        }
        val useCase = GetItemDetailUseCase(mediaRepo)
        val result = useCase("u1", "m1")

        assertTrue(result.isSuccess)
        assertEquals("Inception", result.getOrNull()?.name)
    }

    @Test
    fun `ToggleFavoriteUseCase marks as favorite when currently not favorite`() = runTest {
        var markedId: String? = null
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit> {
                markedId = itemId
                return Result.success(Unit)
            }
        }
        val useCase = ToggleFavoriteUseCase(mediaRepo)
        val result = useCase("u1", "item-123", currentFavorite = false)

        assertTrue(result.isSuccess)
        assertEquals(true, result.getOrNull())
        assertEquals("item-123", markedId)
    }

    @Test
    fun `ToggleFavoriteUseCase unmarks favorite when currently favorite`() = runTest {
        var unmarkedId: String? = null
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit> {
                unmarkedId = itemId
                return Result.success(Unit)
            }
        }
        val useCase = ToggleFavoriteUseCase(mediaRepo)
        val result = useCase("u1", "item-123", currentFavorite = true)

        assertTrue(result.isSuccess)
        assertEquals(false, result.getOrNull())
        assertEquals("item-123", unmarkedId)
    }

    @Test
    fun `TogglePlayedUseCase marks as played when currently unplayed`() = runTest {
        var markedId: String? = null
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit> {
                markedId = itemId
                return Result.success(Unit)
            }
        }
        val useCase = TogglePlayedUseCase(mediaRepo)
        val result = useCase("u1", "item-123", currentPlayed = false)

        assertTrue(result.isSuccess)
        assertEquals(true, result.getOrNull())
        assertEquals("item-123", markedId)
    }

    @Test
    fun `TogglePlayedUseCase marks as unplayed when currently played`() = runTest {
        var unmarkedId: String? = null
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit> {
                unmarkedId = itemId
                return Result.success(Unit)
            }
        }
        val useCase = TogglePlayedUseCase(mediaRepo)
        val result = useCase("u1", "item-123", currentPlayed = true)

        assertTrue(result.isSuccess)
        assertEquals(false, result.getOrNull())
        assertEquals("item-123", unmarkedId)
    }

    @Test
    fun `GetNextEpisodeUseCase returns null when item has no seriesId`() = runTest {
        val movie = MediaItem(id = "m1", name = "Movie", type = MediaItemType.Movie)
        val mediaRepo = object : FakeMediaRepository() {}
        val useCase = GetNextEpisodeUseCase(mediaRepo)
        val result = useCase("u1", movie)

        assertTrue(result.isSuccess)
        assertEquals(null, result.getOrNull())
    }

    @Test
    fun `GetNextEpisodeUseCase finds next episode in same season`() = runTest {
        val ep1 = MediaItem(
            id = "e1",
            name = "Ep 1",
            type = MediaItemType.Episode,
            seriesId = "s1",
            seasonId = "season1",
            indexNumber = 1,
            parentIndexNumber = 1,
        )
        val ep2 = MediaItem(
            id = "e2",
            name = "Ep 2",
            type = MediaItemType.Episode,
            seriesId = "s1",
            seasonId = "season1",
            indexNumber = 2,
            parentIndexNumber = 1,
        )
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> {
                return Result.success(listOf(ep1, ep2))
            }
        }
        val useCase = GetNextEpisodeUseCase(mediaRepo)
        val result = useCase("u1", ep1)

        assertTrue(result.isSuccess)
        assertEquals("e2", result.getOrNull()?.id)
        assertEquals("Ep 2", result.getOrNull()?.name)
    }

    @Test
    fun `GetNextEpisodeUseCase advances to first episode of next season when season finishes`() = runTest {
        val s1e2 = MediaItem(
            id = "e2",
            name = "S1 Finale",
            type = MediaItemType.Episode,
            seriesId = "s1",
            seasonId = "season1",
            indexNumber = 2,
            parentIndexNumber = 1,
        )
        val s2e1 = MediaItem(
            id = "e3",
            name = "S2 Premiere",
            type = MediaItemType.Episode,
            seriesId = "s1",
            seasonId = "season2",
            indexNumber = 1,
            parentIndexNumber = 2,
        )
        val season1 = MediaItem(id = "season1", name = "Season 1", type = MediaItemType.Season, indexNumber = 1)
        val season2 = MediaItem(id = "season2", name = "Season 2", type = MediaItemType.Season, indexNumber = 2)

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> {
                return Result.success(listOf(season1, season2))
            }
            override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> {
                return if (seasonId == "season1") {
                    Result.success(listOf(s1e2))
                } else {
                    Result.success(listOf(s2e1))
                }
            }
        }
        val useCase = GetNextEpisodeUseCase(mediaRepo)
        val result = useCase("u1", s1e2)

        assertTrue(result.isSuccess)
        assertEquals("e3", result.getOrNull()?.id)
        assertEquals("S2 Premiere", result.getOrNull()?.name)
    }

    @Test
    fun `GetHomeFeedUseCase loads sections and selects hero`() = runTest {
        val resumeItem = MediaItem(id = "r1", name = "Resume Movie", type = MediaItemType.Movie)
        val library = MediaItem(id = "lib1", name = "Filmes", type = MediaItemType.CollectionFolder)
        val latestMovie = MediaItem(id = "l1", name = "Latest Movie", type = MediaItemType.Movie)

        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(listOf(resumeItem))
            override suspend fun getLibraries(userId: String) = Result.success(listOf(library))
            override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(listOf(latestMovie))
        }

        val useCase = GetHomeFeedUseCase(mediaRepo)
        val result = useCase("u1")

        assertTrue(result.isSuccess)
        val feed = result.getOrNull()!!
        assertEquals("r1", feed.heroItem?.id)
        assertEquals(listOf(resumeItem), feed.resumeItems)
        assertEquals(listOf(library), feed.libraries)
        assertEquals(listOf(latestMovie), feed.recentlyAddedByLibrary["Filmes"])
    }

    @Test
    fun `GetHomeFeedUseCase loads favorites independently`() = runTest {
        val favorite = MediaItem(id = "fav1", name = "Favorito", type = MediaItemType.Movie)
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItems(
                userId: String,
                parentId: String?,
                includeItemTypes: String?,
                sortBy: String?,
                sortOrder: String?,
                filters: String?,
                searchTerm: String?,
                startIndex: Int,
                limit: Int,
                genres: String?,
                years: String?,
                isPlayed: Boolean?,
                isFavorite: Boolean?,
            ): Result<Pair<List<MediaItem>, Int>> {
                assertEquals("IsFavorite", filters)
                assertEquals("SortName", sortBy)
                assertEquals(12, limit)
                return Result.success(listOf(favorite) to 1)
            }
        }

        val result = GetHomeFeedUseCase(mediaRepo)("u1")

        assertEquals(listOf(favorite), result.getOrThrow().favoriteItems)
    }

    @Test
    fun `SearchMediaUseCase trims query and returns empty list for blank`() = runTest {
        var searchedTerm: String? = null
        val searchRepo = object : SearchRepository {
            override suspend fun searchHints(term: String, userId: String?) = Result.success(emptyList<SearchHintItem>())
            override suspend fun searchItems(term: String, userId: String, itemTypes: String?): Result<List<MediaItem>> {
                searchedTerm = term
                return Result.success(listOf(MediaItem(id = "m1", name = "Search Hit", type = MediaItemType.Movie)))
            }
        }
        val useCase = SearchMediaUseCase(searchRepo)

        val blankResult = useCase("u1", "   ")
        assertTrue(blankResult.isSuccess)
        assertTrue(blankResult.getOrNull()!!.isEmpty())

        val validResult = useCase("u1", "  matrix  ")
        assertTrue(validResult.isSuccess)
        assertEquals("matrix", searchedTerm)
        assertEquals(1, validResult.getOrNull()!!.size)
    }

    @Test
    fun `ManageDownloadsUseCase enforces validation and delegates to repository`() = runTest {
        var enqueuedId: String? = null
        var paused = false
        val downloadRepo = object : DownloadRepository {
            override fun observeDownloads(): Flow<List<DownloadEntry>> = flowOf(emptyList())
            override fun enqueue(id: String, title: String, uri: String): Result<Unit> {
                enqueuedId = id
                return Result.success(Unit)
            }
            override fun retry(id: String, title: String, uri: String): Result<Unit> = Result.success(Unit)
            override fun remove(id: String): Result<Unit> = Result.success(Unit)
            override fun pauseAll(): Result<Unit> {
                paused = true
                return Result.success(Unit)
            }
            override fun resumeAll(): Result<Unit> = Result.success(Unit)
        }
        val useCase = ManageDownloadsUseCase(downloadRepo)

        val enqueueResult = useCase.enqueue("d1", "Title", "https://example.com/video.mp4")
        assertTrue(enqueueResult.isSuccess)
        assertEquals("d1", enqueuedId)

        val pauseResult = useCase.pauseAll()
        assertTrue(pauseResult.isSuccess)
        assertTrue(paused)
    }

    @Test
    fun `GetLiveTvChannelsUseCase loads channels and recordings`() = runTest {
        val channel = MediaItem(id = "ch1", name = "Canal 1", type = MediaItemType.LiveTvChannel)
        val recording = MediaItem(id = "rec1", name = "Recording 1", type = MediaItemType.Movie)
        val liveTvRepo = object : LiveTvRepository {
            override suspend fun getChannels(userId: String) = Result.success(listOf(channel))
            override suspend fun getPrograms(channelIds: List<String>, minStartDate: String?, maxEndDate: String?) = Result.success(emptyList<MediaItem>())
            override suspend fun getRecordings(userId: String) = Result.success(listOf(recording))
            override suspend fun scheduleRecording(program: MediaItem) = Result.success(Unit)
        }
        val useCase = GetLiveTvChannelsUseCase(liveTvRepo)
        val result = useCase("u1")

        assertTrue(result.isSuccess)
        val guide = result.getOrNull()!!
        assertEquals(listOf(channel), guide.channels)
        assertEquals(listOf(recording), guide.recordings)
    }

    @Test
    fun `GetLibraryItemsUseCase validates parameters and delegates`() = runTest {
        val item = MediaItem(id = "m1", name = "Filme 1", type = MediaItemType.Movie)
        val mediaRepo = object : FakeMediaRepository() {
            override suspend fun getItems(
                userId: String,
                parentId: String?,
                includeItemTypes: String?,
                sortBy: String?,
                sortOrder: String?,
                filters: String?,
                searchTerm: String?,
                startIndex: Int,
                limit: Int,
                genres: String?,
                years: String?,
                isPlayed: Boolean?,
                isFavorite: Boolean?,
            ): Result<Pair<List<MediaItem>, Int>> {
                return Result.success(listOf(item) to 1)
            }
        }
        val useCase = GetLibraryItemsUseCase(mediaRepo)
        val blankUser = useCase("", "lib-1")
        assertTrue(blankUser.isFailure)

        val success = useCase("u1", "lib-1", startIndex = 0, limit = 20)
        assertTrue(success.isSuccess)
        assertEquals(1, success.getOrNull()!!.first.size)
    }

    @Test
    fun `ManageSyncPlayUseCase coordinates groups correctly`() = runTest {
        val group = SyncPlayGroup(
            groupId = "g1",
            groupName = "Mulleta Room",
            state = "Playing",
            participants = listOf("u1"),
            playingItemId = "item1",
            positionTicks = 0L,
        )
        val repo = object : SyncPlayRepository {
            override suspend fun getGroups() = Result.success(listOf(group))
            override suspend fun createGroup(name: String) = Result.success(Unit)
            override suspend fun joinGroup(groupId: String) = Result.success(Unit)
            override suspend fun leaveGroup() = Result.success(Unit)
        }
        val useCase = ManageSyncPlayUseCase(repo)

        val groups = useCase.getGroups()
        assertTrue(groups.isSuccess)
        assertEquals(1, groups.getOrNull()!!.size)

        val blankCreate = useCase.createGroup("  ")
        assertTrue(blankCreate.isFailure)

        val validCreate = useCase.createGroup("Room 1")
        assertTrue(validCreate.isSuccess)
    }

    @Test
    fun `ManagePlaylistUseCase validates name and delegates`() = runTest {
        val playlist = Playlist(id = "p1", name = "Favoritos Rock")
        val repo = object : PlaylistRepository {
            override suspend fun getPlaylists(userId: String) = Result.success(listOf(playlist))
            override suspend fun createPlaylist(userId: String, name: String, itemId: String?) = Result.success(playlist)
            override suspend fun addItem(userId: String, playlistId: String, itemId: String) = Result.success(Unit)
        }
        val useCase = ManagePlaylistUseCase(repo)

        val playlists = useCase.getPlaylists("u1")
        assertTrue(playlists.isSuccess)
        assertEquals(1, playlists.getOrNull()!!.size)

        val blankCreate = useCase.createPlaylist("u1", "  ")
        assertTrue(blankCreate.isFailure)

        val validCreate = useCase.createPlaylist("u1", "Rock Clássico")
        assertTrue(validCreate.isSuccess)
    }

    @Test
    fun `SwitchUserUseCase logs into new account`() = runTest {
        var loggedUser: String? = null
        val authRepo = object : FakeAuthRepository() {
            override suspend fun login(username: String, password: String): Result<UserSession> {
                loggedUser = username
                return Result.success(UserSession(userId = "u2", userName = username, token = "token", serverId = "srv-1"))
            }
        }
        val useCase = SwitchUserUseCase(authRepo)

        val blank = useCase("  ", "secret")
        assertTrue(blank.isFailure)

        val result = useCase("raphael", "secret")
        assertTrue(result.isSuccess)
        assertEquals("raphael", loggedUser)
    }

    @Test
    fun `LoginUseCase validates non-blank username and executes login`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun login(username: String, password: String): Result<UserSession> {
                return Result.success(UserSession(userId = "u1", userName = username, token = "token", serverId = "srv1"))
            }
        }
        val useCase = LoginUseCase(authRepo)

        assertTrue(useCase("  ", "pass").isFailure)

        val success = useCase("user1", "pass")
        assertTrue(success.isSuccess)
        assertEquals("user1", success.getOrNull()?.userName)
    }

    @Test
    fun `RegisterUseCase enforces validation rules and executes registration`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun register(username: String, password: String): Result<RegistrationResult> {
                return Result.success(RegistrationResult(true, "Cadastrado"))
            }
        }
        val useCase = RegisterUseCase(authRepo)

        assertTrue(useCase("", "12345678").isFailure)
        assertTrue(useCase("valid_user", "short").isFailure)

        val success = useCase("valid_user", "12345678")
        assertTrue(success.isSuccess)
        assertTrue(success.getOrNull()!!.success)
    }

    @Test
    fun `VerifyServerUseCase validates url and queries server`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun verifyServer(url: String): Result<ServerVerification> {
                return Result.success(ServerVerification("Server 1", "10.9", 20L))
            }
        }
        val useCase = VerifyServerUseCase(authRepo)

        assertTrue(useCase("  ").isFailure)

        val success = useCase("http://mulletaflix.local:8096")
        assertTrue(success.isSuccess)
        assertEquals("Server 1", success.getOrNull()?.name)
    }

    private open class FakeAuthRepository : AuthRepository {
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
        override fun getSavedUserId(): Flow<String?> = flowOf("u1")
        override fun getSavedUserName(): Flow<String?> = flowOf("Test User")
        override fun getSavedToken(): Flow<String?> = flowOf("token-123")
    }

    private open class FakeMediaRepository : MediaRepository {
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
}
