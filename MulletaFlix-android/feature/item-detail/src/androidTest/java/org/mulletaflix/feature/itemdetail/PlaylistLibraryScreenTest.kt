package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertEquals
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.repository.*
import org.mulletaflix.domain.usecase.ManagePlaylistUseCase

@RunWith(AndroidJUnit4::class)
class PlaylistLibraryScreenTest {
    @get:Rule val composeRule = createComposeRule()

    @Test
    fun displaysPlaylistMediaAndPlaybackAction() {
        val playlist = Playlist("p1", "Favoritos")
        val repository = TestPlaylistRepository(listOf(playlist), listOf(MediaItem("m1", "Meu filme", MediaItemType.Movie)))
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(repository), testAuthRepository())

        composeRule.setContent {
            MaterialTheme {
                PlaylistLibraryScreen(onBack = {}, onItemClick = {}, onPlay = {}, viewModel = viewModel)
            }
        }

        composeRule.onNodeWithText("Favoritos").assertExists()
        composeRule.onNodeWithText("Meu filme").assertExists()
        composeRule.onNodeWithContentDescription("Reproduzir Meu filme").assertExists()
    }

    @Test
    fun hidesBookAndAudiobookTitlesOnAndroidTv() {
        assumeTelevisionAvd()
        val playlist = Playlist("p1", "Favoritos")
        val repository = TestPlaylistRepository(
            listOf(playlist),
            listOf(
                MediaItem("movie", "Filme visível", MediaItemType.Movie),
                MediaItem("book", "Livro oculto", MediaItemType.Book),
                MediaItem("audiobook", "Audiolivro oculto", MediaItemType.AudioBook),
            ),
        )
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(repository), testAuthRepository())

        composeRule.setContent {
            MaterialTheme {
                PlaylistLibraryScreen(onBack = {}, onItemClick = {}, onPlay = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(5_000) { viewModel.state.value.items.size == 3 }
        composeRule.onNodeWithText("Filme visível").assertExists()
        composeRule.onNodeWithText("Livro oculto").assertDoesNotExist()
        composeRule.onNodeWithText("Audiolivro oculto").assertDoesNotExist()
        composeRule.onNodeWithText("Favoritos · 1 título visível").assertExists()
    }

    @Test
    fun tvPlaylistOffersPaginationWhenCurrentPageContainsOnlyBooks() {
        assumeTelevisionAvd()
        val playlist = Playlist("p1", "Favoritos")
        val repository = object : PlaylistRepository {
            override suspend fun getPlaylists(userId: String) = Result.success(listOf(playlist))
            override suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int): Result<Pair<List<MediaItem>, Int>> =
                if (startIndex == 0) {
                    Result.success(listOf(MediaItem("book", "Livro oculto", MediaItemType.Book)) to 2)
                } else {
                    Result.success(listOf(MediaItem("movie", "Filme na página seguinte", MediaItemType.Movie)) to 2)
                }
            override suspend fun createPlaylist(userId: String, name: String, itemId: String?) = Result.success(playlist)
            override suspend fun addItem(userId: String, playlistId: String, itemId: String) = Result.success(Unit)
        }
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(repository), testAuthRepository())

        composeRule.setContent {
            MaterialTheme {
                PlaylistLibraryScreen(onBack = {}, onItemClick = {}, onPlay = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(5_000) { viewModel.state.value.items.size == 1 }
        composeRule.onNodeWithText("Livro oculto").assertDoesNotExist()
        composeRule.onNodeWithText("Carregar mais").assertExists().performClick()
        composeRule.waitUntil(5_000) { viewModel.state.value.items.any { it.id == "movie" } }
        composeRule.onNodeWithText("Filme na página seguinte").assertExists()
    }

    @Test
    fun explainsWhenThereAreNoPlaylists() {
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(TestPlaylistRepository()), testAuthRepository())
        composeRule.setContent {
            MaterialTheme {
                PlaylistLibraryScreen(onBack = {}, onItemClick = {}, onPlay = {}, viewModel = viewModel)
            }
        }

        composeRule.onNodeWithText("Nenhuma playlist encontrada").assertExists()
        composeRule.onNodeWithText("Crie uma playlist nos detalhes de um título.").assertExists()
    }

    @Test
    fun keepsLoadedTitlesVisibleAndRetriesTheFailedPage() {
        val playlist = Playlist("p1", "Favoritos")
        val first = MediaItem("m1", "Título inicial", MediaItemType.Movie)
        val second = MediaItem("m2", "Título seguinte", MediaItemType.Movie)
        val starts = mutableListOf<Int>()
        var failNextPage = true
        val retryRelease = CompletableDeferred<Unit>()
        val repository = object : PlaylistRepository {
            override suspend fun getPlaylists(userId: String) = Result.success(listOf(playlist))
            override suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int): Result<Pair<List<MediaItem>, Int>> {
                starts += startIndex
                if (startIndex == 0) return Result.success(listOf(first) to 2)
                if (failNextPage) {
                    failNextPage = false
                    return Result.failure(IllegalStateException("Falha temporária"))
                }
                retryRelease.await()
                return Result.success(listOf(second) to 2)
            }
            override suspend fun createPlaylist(userId: String, name: String, itemId: String?) = Result.success(playlist)
            override suspend fun addItem(userId: String, playlistId: String, itemId: String) = Result.success(Unit)
        }
        val viewModel = PlaylistLibraryViewModel(ManagePlaylistUseCase(repository), testAuthRepository())
        composeRule.setContent {
            MaterialTheme {
                PlaylistLibraryScreen(onBack = {}, onItemClick = {}, onPlay = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(5_000) { viewModel.state.value.items.size == 1 }
        composeRule.onNodeWithText("Carregar mais").performClick()
        composeRule.waitUntil(5_000) { viewModel.state.value.itemsError != null }

        composeRule.onNodeWithText("Título inicial").assertExists()
        composeRule.onNodeWithText("Falha temporária").assertExists()
        composeRule.onNodeWithText("Tentar novamente").performClick()
        composeRule.waitUntil(5_000) { viewModel.state.value.isLoadingItems }
        composeRule.onNodeWithContentDescription("Carregando mais títulos").assertExists()
        composeRule.onNodeWithText("Título inicial").assertExists()
        retryRelease.complete(Unit)
        composeRule.waitUntil(5_000) { viewModel.state.value.items.size == 2 }

        composeRule.onNodeWithText("Título inicial").assertExists()
        composeRule.onNodeWithText("Título seguinte").assertExists()
        assertEquals(listOf(0, 1, 1), starts)
    }
}

private fun assumeTelevisionAvd() {
    val expectedProfile = InstrumentationRegistry.getArguments().getString("expectedDeviceProfile")
    val configuration = InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration
    val isTelevision = (configuration.uiMode and android.content.res.Configuration.UI_MODE_TYPE_MASK) ==
        android.content.res.Configuration.UI_MODE_TYPE_TELEVISION
    assumeTrue("This test is specific to an Android TV AVD", expectedProfile == "TV" && isTelevision)
}

private class TestPlaylistRepository(
    private val playlists: List<Playlist> = emptyList(),
    private val media: List<MediaItem> = emptyList(),
) : PlaylistRepository {
    override suspend fun getPlaylists(userId: String) = Result.success(playlists)
    override suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int) = Result.success(media to media.size)
    override suspend fun createPlaylist(userId: String, name: String, itemId: String?) = Result.success(Playlist("new", name))
    override suspend fun addItem(userId: String, playlistId: String, itemId: String) = Result.success(Unit)
}

private fun testAuthRepository() = object : AuthRepository {
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
