package org.mulletaflix.feature.search

import android.content.res.Configuration
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.SearchHintItem
import org.mulletaflix.domain.repository.SearchHistoryRepository
import org.mulletaflix.domain.repository.SearchRepository
import org.mulletaflix.domain.repository.SearchResults
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.SearchMediaUseCase

@RunWith(AndroidJUnit4::class)
class SearchScreenDeviceVisibilityTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun televisionSearchDoesNotRenderBookOrAudiobookResults() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        assertEquals("TV", InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"))
        assertEquals(
            Configuration.UI_MODE_TYPE_TELEVISION,
            instrumentation.targetContext.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK,
        )

        val repository = TvSearchRepository()
        val viewModel = SearchViewModel(
            SearchMediaUseCase(repository),
            TvAuthRepository(),
            TvSearchHistoryRepository(),
            object : NetworkMonitor {
                override val isOnline: Flow<Boolean> = flowOf(true)
            },
        )

        composeRule.setContent {
            MulletaFlixTheme {
                SearchScreen(onItemClick = {}, viewModel = viewModel)
            }
        }
        composeRule.runOnIdle { viewModel.onQueryChange("Duna") }
        composeRule.waitUntil(5_000) {
            val state = viewModel.state.value
            state.results.size == 3 && !state.isLoading
        }

        composeRule.onNodeWithText("Filme nos resultados").assertExists()
        composeRule.onNodeWithText("Livro nos resultados").assertDoesNotExist()
        composeRule.onNodeWithText("Audiolivro nos resultados").assertDoesNotExist()
    }

    @Test
    fun televisionSuggestionsDoNotRenderBookOrAudiobookHints() {
        val hints = searchHintsForDevice(
            listOf(
                SearchHintItem("movie-hint", "Filme nas sugestões", "Movie", 2025, null),
                SearchHintItem("book-hint", "Livro nas sugestões", "Book", 2025, null),
                SearchHintItem("audiobook-hint", "Audiolivro nas sugestões", "AudioBook", 2025, null),
            ),
            isTelevision = true,
        )

        composeRule.setContent {
            MulletaFlixTheme {
                SearchHintPanel(
                    hints = hints,
                    isLoading = false,
                    onHintClick = {},
                    focusFriendly = true,
                )
            }
        }

        composeRule.onNodeWithText("Livro nas sugestões").assertDoesNotExist()
        composeRule.onNodeWithText("Audiolivro nas sugestões").assertDoesNotExist()
        composeRule.onNodeWithText("Filme nas sugestões").assertExists()
    }
}

private class TvSearchRepository : SearchRepository {
    private val items = listOf(
        MediaItem("movie", "Filme nos resultados", MediaItemType.Movie),
        MediaItem("book", "Livro nos resultados", MediaItemType.Book),
        MediaItem("audiobook", "Audiolivro nos resultados", MediaItemType.AudioBook),
    )

    override suspend fun searchHints(term: String, userId: String?) = Result.success(
        listOf(
            SearchHintItem("movie-hint", "Filme nas sugestões", "Movie", 2025, null),
            SearchHintItem("book-hint", "Livro nas sugestões", "Book", 2025, null),
            SearchHintItem("audiobook-hint", "Audiolivro nas sugestões", "AudioBook", 2025, null),
        ),
    )

    override suspend fun searchItems(
        term: String,
        userId: String,
        itemTypes: String?,
        startIndex: Int,
    ) = Result.success(SearchResults(items, items.size))
}

private class TvSearchHistoryRepository : SearchHistoryRepository {
    override fun observeHistory(userId: String?) = flowOf(emptyList<String>())
    override suspend fun add(userId: String?, query: String) = Unit
    override suspend fun remove(userId: String?, query: String) = Unit
    override suspend fun clear(userId: String?) = Unit
}

private class TvAuthRepository : AuthRepository {
    override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Test", "1"))
    override suspend fun register(username: String, password: String) = Result.success(RegistrationResult(true))
    override suspend fun login(username: String, password: String) = Result.success(UserSession("user", username, "token", null))
    override suspend fun getAvailableUsers() = Result.success(emptyList<AvailableUser>())
    override suspend fun initiateQuickConnect() = Result.success(QuickConnectState("1234", "secret", false))
    override suspend fun checkQuickConnect(secret: String) = Result.success<UserSession?>(null)
    override suspend fun logout() = Result.success(Unit)
    override fun getSavedServerUrl() = flowOf("https://example.test")
    override suspend fun setServerUrl(url: String) = Unit
    override fun getSavedUserId() = flowOf("user")
    override fun getSavedToken() = flowOf("token")
}
