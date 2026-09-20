package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assert
import androidx.compose.ui.test.hasScrollAction
import androidx.compose.ui.test.junit4.v2.createComposeRule
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class MetadataPillsTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun metadataBadgesExposeHorizontalScroll() {
        composeRule.setContent {
            MaterialTheme {
                MetadataPills(
                    item = MediaItem(
                        id = "movie-1",
                        name = "Filme de teste",
                        type = MediaItemType.Movie,
                        year = 2026,
                        officialRating = "16",
                        runtimeTicks = 7_200_000_000L,
                        communityRating = 8.7f,
                        has4K = true,
                        hasHdr = true,
                        hasAtmos = true,
                    ),
                )
            }
        }

        composeRule.onNode(hasScrollAction()).assert(hasScrollAction())
    }
}
