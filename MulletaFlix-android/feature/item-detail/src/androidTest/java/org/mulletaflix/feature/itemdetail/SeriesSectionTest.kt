package org.mulletaflix.feature.itemdetail

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * Regression test for the crash reported when opening a series from Home:
 * the season selector was rendered with zero tabs while the seasons request
 * was still in flight, and `PrimaryScrollableTabRow` throws
 * `IndexOutOfBoundsException: Index 0 out of bounds for length 0`.
 */
@RunWith(AndroidJUnit4::class)
class SeriesSectionTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun rendersEmptyStateInsteadOfCrashingWhenNoSeasonIsAvailable() {
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = emptyList(),
                    episodes = emptyList(),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                )
            }
        }

        composeRule.onNodeWithText("Nenhuma temporada disponível").assertIsDisplayed()
    }

    @Test
    fun rendersSeasonCoversAndEpisodesWhenSeasonsAreAvailable() {
        val seasons = listOf(
            org.mulletaflix.domain.model.MediaItem("sea-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season),
            org.mulletaflix.domain.model.MediaItem("sea-2", "Temporada 2", org.mulletaflix.domain.model.MediaItemType.Season),
        )
        val episodes = listOf(
            org.mulletaflix.domain.model.MediaItem(
                "ep-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                indexNumber = 1, parentIndexNumber = 1,
            )
        )

        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = seasons,
                    episodes = episodes,
                    selectedSeasonIndex = 1,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                )
            }
        }

        // Season covers carry the season name; the selected one is exposed as
        // selected for accessibility.
        composeRule.onNodeWithText("Temporada 1").assertIsDisplayed()
        composeRule.onNodeWithText("Temporada 2").assertIsDisplayed().assertIsSelected()
        composeRule.onNodeWithText("1x01 Piloto").assertIsDisplayed()
    }

    @Test
    fun rendersEpisodeErrorAndRetryAction() {
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "sea-1",
                            "Temporada 1",
                            org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = emptyList(),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                    error = "Não foi possível carregar os episódios.",
                    onRetry = {},
                )
            }
        }

        composeRule.onNodeWithText("Não foi possível carregar os episódios.").assertIsDisplayed()
        composeRule.onNodeWithText("Tentar novamente").assertIsDisplayed()
    }
}
