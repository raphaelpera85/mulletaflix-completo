package org.mulletaflix.feature.home

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.Assert.assertTrue
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

@RunWith(AndroidJUnit4::class)
class HomeOfflineStatusCardTest {
    @get:Rule val composeRule = createComposeRule()

    @Test fun cachedHomeExplainsFreshnessAndPlaybackRequirement() {
        composeRule.setContent {
            MulletaFlixTheme {
                HomeOfflineStatusCard(
                    cachedAtEpochMillis = 1_700_000_000_000,
                    resumeCached = true,
                    favoritesCached = true,
                )
            }
        }

        composeRule.onNodeWithText("Sem conexão · Continuar Assistindo e Favoritos em cache desde", substring = true)
            .assertIsDisplayed()
        composeRule.onNodeWithText("Apenas mídias baixadas podem ser reproduzidas offline.", substring = true)
            .assertIsDisplayed()
    }

    @Test fun coldOfflineHomeExplainsThereIsNoSavedFeed() {
        composeRule.setContent {
            MulletaFlixTheme {
                HomeOfflineStatusCard(cachedAtEpochMillis = null, resumeCached = false, favoritesCached = false)
            }
        }

        composeRule.onNodeWithText(
            "Você está offline e não há conteúdo da Home salvo. Acesse Downloads para reproduzir mídias baixadas."
        ).assertIsDisplayed()
    }

    @Test fun televisionOfflineMessageDoesNotReferToDownloads() {
        composeRule.setContent {
            MulletaFlixTheme {
                HomeOfflineStatusCard(
                    cachedAtEpochMillis = null,
                    downloadsAvailable = false,
                )
            }
        }

        composeRule.onNodeWithText("Você está offline e não há conteúdo da Home salvo.").assertIsDisplayed()
        composeRule.onAllNodesWithText("Downloads").assertCountEquals(0)
    }

    @Test fun partialHomeCacheNamesOnlyTheSavedSection() {
        composeRule.setContent {
            MulletaFlixTheme {
                HomeOfflineStatusCard(
                    cachedAtEpochMillis = 1_700_000_000_000,
                    resumeCached = false,
                    favoritesCached = true,
                )
            }
        }

        composeRule.onNodeWithText("Sem conexão · Favoritos em cache desde", substring = true).assertIsDisplayed()
        assertTrue(composeRule.onAllNodesWithText("Continuar Assistindo em cache", substring = true).fetchSemanticsNodes().isEmpty())
    }
}
