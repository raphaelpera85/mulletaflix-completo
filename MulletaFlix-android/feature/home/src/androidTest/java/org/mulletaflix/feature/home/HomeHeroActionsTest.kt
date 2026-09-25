package org.mulletaflix.feature.home

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

@RunWith(AndroidJUnit4::class)
class HomeHeroActionsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun playAndMoreInfoInvokeDifferentActionsForTheFeaturedTitle() {
        val actions = mutableListOf<String>()
        composeRule.setContent {
            MaterialTheme {
                HeroBanner(
                    item = MediaItem("featured-123", "Título em destaque", MediaItemType.Movie),
                    heightDp = 500,
                    onPlay = { actions += "play:featured-123" },
                    onMoreInfo = { actions += "details:featured-123" },
                )
            }
        }

        composeRule.onNodeWithText("Reproduzir").performClick()
        composeRule.onNodeWithText("Mais Informações").performClick()

        composeRule.runOnIdle {
            assertEquals(
                listOf("play:featured-123", "details:featured-123"),
                actions,
            )
        }
    }
}
