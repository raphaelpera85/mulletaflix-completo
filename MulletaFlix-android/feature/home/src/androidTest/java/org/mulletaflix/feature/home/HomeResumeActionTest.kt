package org.mulletaflix.feature.home

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

@RunWith(AndroidJUnit4::class)
class HomeResumeActionTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun resumeAndDetailsRemainSeparateAndItemsWithoutProgressHaveNoResumeAction() {
        val actions = mutableListOf<String>()
        composeRule.setContent {
            MaterialTheme {
                MediaSection(
                    title = "Continuar Assistindo",
                    items = listOf(
                        MediaItem(
                            id = "in-progress",
                            name = "Filme em andamento",
                            type = MediaItemType.Movie,
                            playedPercentage = 42.0,
                            playbackPositionTicks = 42_000_000L,
                        ),
                        MediaItem(
                            id = "not-started",
                            name = "Filme não iniciado",
                            type = MediaItemType.Movie,
                        ),
                    ),
                    cardShape = null,
                    cardWidth = null,
                    layoutSpec = homeLayoutSpec(HomeDeviceClass.PHONE),
                    onItemClick = { actions += "details:$it" },
                    onResumeItemClick = { actions += "resume:$it" },
                )
            }
        }

        composeRule.onAllNodesWithText("Retomar").assertCountEquals(1)
        composeRule.onNodeWithContentDescription("Abrir Filme em andamento, 42% reproduzido")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithContentDescription("Retomar Filme em andamento")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithContentDescription("Abrir Filme não iniciado").performClick()

        composeRule.runOnIdle {
            assertEquals(
                listOf("details:in-progress", "resume:in-progress", "details:not-started"),
                actions,
            )
        }
    }

    @Test
    fun focusedResumeButtonStartsTheMatchingItemWithOneCentrePressOnTv() {
        assumeTelevisionProfile()
        val resumedIds = mutableListOf<String>()
        composeRule.setContent {
            MaterialTheme {
                MediaSection(
                    title = "Continuar Assistindo",
                    items = listOf(
                        MediaItem(
                            id = "tv-resume",
                            name = "Filme para TV",
                            type = MediaItemType.Movie,
                            playedPercentage = 25.0,
                            playbackPositionTicks = 25_000_000L,
                        ),
                    ),
                    cardShape = null,
                    cardWidth = null,
                    layoutSpec = homeLayoutSpec(HomeDeviceClass.TV),
                    onItemClick = {},
                    onResumeItemClick = { resumedIds += it },
                )
            }
        }

        val resumeButton = composeRule.onNodeWithContentDescription("Retomar Filme para TV")
        resumeButton.assertIsDisplayed()
        resumeButton.requestFocus()
        resumeButton.assertIsFocused()
        resumeButton.performKeyInput { pressKey(Key.DirectionCenter) }

        composeRule.runOnIdle {
            assertEquals(listOf("tv-resume"), resumedIds)
            assertTrue(resumedIds.size == 1)
        }
    }
}
