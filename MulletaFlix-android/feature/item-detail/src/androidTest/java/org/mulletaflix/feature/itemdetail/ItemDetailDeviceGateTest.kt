package org.mulletaflix.feature.itemdetail

import android.content.res.Configuration
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class ItemDetailDeviceGateTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun bookDeepLinkDoesNotRenderTitleOnTelevisionAndOffersBackAction() {
        assumeTelevisionAvd()
        val item = MediaItem("book-1", "Título confidencial", MediaItemType.Book)
        var backCalls = 0

        composeRule.setContent {
            MulletaFlixTheme {
                ItemDetailDeviceGate(
                    item = item,
                    isTelevision = isTelevisionConfiguration(),
                    onBack = { backCalls++ },
                ) {
                    Text(item.name, modifier = Modifier.testTag("item-detail-title"))
                }
            }
        }

        composeRule.onNodeWithText("Conteúdo de livros indisponível na Android TV").assertIsDisplayed()
        composeRule.onNodeWithTag("item-detail-title").assertDoesNotExist()
        composeRule.onNodeWithTag(ITEM_DETAIL_TV_BOOK_BACK_TEST_TAG).assertIsDisplayed().performClick()
        composeRule.runOnIdle { assertEquals(1, backCalls) }
    }

    @Test
    fun audiobookDeepLinkDoesNotRenderTitleOnTelevision() {
        assumeTelevisionAvd()
        val item = MediaItem("audio-book-1", "Audiolivro privado", MediaItemType.AudioBook)

        composeRule.setContent {
            MulletaFlixTheme {
                ItemDetailDeviceGate(item = item, isTelevision = isTelevisionConfiguration(), onBack = {}) {
                    Text(item.name, modifier = Modifier.testTag("item-detail-title"))
                }
            }
        }

        composeRule.onNodeWithTag("item-detail-title").assertDoesNotExist()
    }

    @Test
    fun similarRecommendationsHideBooksOnTelevisionAndKeepMovies() {
        assumeTelevisionAvd()
        val recommendations = listOf(
            MediaItem("movie-1", "Filme recomendado", MediaItemType.Movie),
            MediaItem("book-1", "Livro recomendado", MediaItemType.Book),
            MediaItem("audio-book-1", "Audiolivro recomendado", MediaItemType.AudioBook),
        )

        composeRule.setContent {
            MulletaFlixTheme {
                SimilarItemsForDevice(
                    items = recommendations,
                    isTelevision = isTelevisionConfiguration(),
                    onItemClick = {},
                )
            }
        }

        composeRule.onNodeWithText("Filme recomendado").assertIsDisplayed()
        composeRule.onNodeWithText("Livro recomendado").assertDoesNotExist()
        composeRule.onNodeWithText("Audiolivro recomendado").assertDoesNotExist()
    }

    @Test
    fun handheldStillRendersBookDetails() {
        val item = MediaItem("book-1", "Livro no celular", MediaItemType.Book)

        composeRule.setContent {
            MaterialTheme {
                ItemDetailDeviceGate(
                    item = item,
                    isTelevision = false,
                    onBack = {},
                ) {
                    Text(item.name, modifier = Modifier.fillMaxSize().testTag("item-detail-title"))
                }
            }
        }

        composeRule.onNodeWithTag("item-detail-title").assertIsDisplayed()
        composeRule.onNodeWithText("Conteúdo de livros indisponível na Android TV").assertDoesNotExist()
    }

    private fun assumeTelevisionAvd() {
        val expectedProfile = InstrumentationRegistry.getArguments().getString("expectedDeviceProfile")
        val actualConfiguration = InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration
        val isTelevision = (actualConfiguration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        assumeTrue("This test is specific to an Android TV AVD", expectedProfile == "TV" && isTelevision)
    }

    @androidx.compose.runtime.Composable
    private fun isTelevisionConfiguration(): Boolean =
        (LocalConfiguration.current.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
}
