package org.mulletaflix.feature.search

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Rule
import org.junit.Test

/**
 * O aviso de truncamento e o "carregar mais".
 *
 * A busca pedia uma página ao servidor e descartava o `TotalRecordCount` da resposta: 30
 * resultados apareciam como se fossem a biblioteca inteira. Na v1.2.86 a tela ganhou a
 * saída que faltava — o resto da resposta vem a pedido.
 */
class SearchTruncationBannerTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun truncatedSearchShowsHowManyOfTheTotalAreOnScreen() {
        composeRule.setContent {
            MaterialTheme {
                searchTruncationNotice(shownCount = 30, totalMatching = 412)
                    ?.let { SearchTruncationBanner(it) }
            }
        }

        composeRule
            .onNodeWithText("Mostrando 30 de 412 resultados.")
            .assertIsDisplayed()
    }

    @Test
    fun completeSearchShowsNoBanner() {
        composeRule.setContent {
            MaterialTheme {
                searchTruncationNotice(shownCount = 3, totalMatching = 3)
                    ?.let { SearchTruncationBanner(it) }
            }
        }

        composeRule.onNodeWithText("Mostrando", substring = true).assertDoesNotExist()
    }

    @Test
    fun unknownTotalShowsNoBanner() {
        composeRule.setContent {
            MaterialTheme {
                searchTruncationNotice(shownCount = 30, totalMatching = null)
                    ?.let { SearchTruncationBanner(it) }
            }
        }

        composeRule.onNodeWithText("Mostrando", substring = true).assertDoesNotExist()
    }

    @Test
    fun loadMoreOffersTheRestOfTheResults() {
        var clicked = false
        composeRule.setContent {
            MaterialTheme {
                LoadMoreRow(isLoading = false, onLoadMore = { clicked = true })
            }
        }

        composeRule.onNodeWithText("Carregar mais").assertIsDisplayed().performClick()
        check(clicked)
    }

    @Test
    fun loadMoreCannotBePressedTwiceWhileAPageIsOnTheWay() {
        composeRule.setContent {
            MaterialTheme {
                LoadMoreRow(isLoading = true, onLoadMore = { error("não deveria ser clicável") })
            }
        }

        // Enquanto a página anterior não chegou, o botão sai de cena: dois toques
        // pediriam a mesma página duas vezes.
        composeRule.onNodeWithText("Carregar mais").assertDoesNotExist()
        composeRule.onNodeWithTag(LOAD_MORE_TEST_TAG).assertExists()
    }
}
