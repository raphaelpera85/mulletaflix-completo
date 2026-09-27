package org.mulletaflix.feature.itemdetail

import android.content.res.Configuration
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertHeightIsAtLeast
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.assertWidthIsAtLeast
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue
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

    @Test
    fun selectedSeasonExposesAnAccessibleBatchDownloadAction() {
        var downloadRequests = 0
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "season-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "episode-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                            indexNumber = 1, parentIndexNumber = 1,
                        ),
                    ),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                    onDownloadSeason = { downloadRequests++ },
                )
            }
        }

        composeRule.onNodeWithText("Baixar temporada")
            .assertIsDisplayed()
            .assertHasClickAction()
            .performClick()

        org.junit.Assert.assertEquals(1, downloadRequests)
    }

    @Test
    fun batchDownloadShowsProgressAndAccessibleCancelAction() {
        var cancelRequests = 0
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "season-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "episode-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                            indexNumber = 1, parentIndexNumber = 1,
                        ),
                    ),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                    seasonDownloadProgress = SeasonDownloadProgress(
                        seasonId = "season-1",
                        seasonName = "Temporada 1",
                        totalEpisodes = 2,
                        processedEpisodes = 1,
                        queuedEpisodes = 1,
                        isRunning = true,
                    ),
                    onCancelSeasonDownload = { cancelRequests++ },
                )
            }
        }

        composeRule.onNodeWithText("Preparando 1/2").assertIsDisplayed()
        composeRule.onNode(
            SemanticsMatcher.expectValue(
                SemanticsProperties.LiveRegion,
                LiveRegionMode.Polite,
            ) and SemanticsMatcher.expectValue(
                SemanticsProperties.ProgressBarRangeInfo,
                ProgressBarRangeInfo(1f, 0f..2f),
            ),
        ).assertExists()
        composeRule.onNodeWithText("Cancelar").assertHasClickAction().performClick()
        org.junit.Assert.assertEquals(1, cancelRequests)
    }

    @Test
    fun batchDownloadActionsExposeEnabledButtonSemantics() {
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "season-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "episode-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                            indexNumber = 1, parentIndexNumber = 1,
                        ),
                    ),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                    seasonDownloadProgress = SeasonDownloadProgress(
                        seasonId = "season-1",
                        seasonName = "Temporada 1",
                        totalEpisodes = 2,
                        processedEpisodes = 1,
                        queuedEpisodes = 1,
                        isRunning = true,
                    ),
                    onCancelSeasonDownload = {},
                )
            }
        }

        composeRule.onNodeWithText("Preparando 1/2").assertIsNotEnabled()
        composeRule.onNodeWithText("Cancelar")
            .assertIsDisplayed()
            .assertIsEnabled()
            .assertHasClickAction()
    }

    @Test
    fun seasonDownloadAndCancelActionsCanBeFocusedAndActivatedWithTvDpad() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val isTelevision =
            (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
                Configuration.UI_MODE_TYPE_TELEVISION
        assumeTrue("D-pad focus behavior is specific to Android TV", isTelevision)

        var downloadRequests = 0
        var cancelRequests = 0
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "season-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "episode-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                            indexNumber = 1, parentIndexNumber = 1,
                        ),
                    ),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                    onDownloadSeason = { downloadRequests++ },
                    seasonDownloadProgress = SeasonDownloadProgress(
                        seasonId = "season-1",
                        seasonName = "Temporada 1",
                        totalEpisodes = 2,
                        processedEpisodes = 1,
                        queuedEpisodes = 1,
                        isRunning = true,
                    ),
                    onCancelSeasonDownload = { cancelRequests++ },
                )
            }
        }

        val selectedSeason = composeRule.onNodeWithText("Temporada 1")
        selectedSeason.requestFocus()
        selectedSeason.assertIsFocused()
        selectedSeason.performKeyInput { pressKey(Key.DirectionDown) }
        val cancelButton = composeRule.onNodeWithText("Cancelar")
        cancelButton.assertIsFocused()
        cancelButton.performKeyInput { pressKey(Key.DirectionCenter) }
        org.junit.Assert.assertEquals(1, cancelRequests)

        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "season-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "episode-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                            indexNumber = 1, parentIndexNumber = 1,
                        ),
                    ),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                    onDownloadSeason = { downloadRequests++ },
                )
            }
        }

        val selectedSeasonWithoutProgress = composeRule.onNodeWithText("Temporada 1")
        selectedSeasonWithoutProgress.requestFocus()
        selectedSeasonWithoutProgress.assertIsFocused()
        selectedSeasonWithoutProgress.performKeyInput { pressKey(Key.DirectionDown) }
        val downloadButton = composeRule.onNodeWithText("Baixar temporada")
        downloadButton.assertIsFocused()
        downloadButton.performKeyInput { pressKey(Key.DirectionCenter) }
        org.junit.Assert.assertEquals(1, downloadRequests)
    }

    /**
     * O botão de play sobre a capa do episódio media 40 dp de alvo — abaixo do mínimo
     * de 48 dp do Material, e justamente sobre a arte que o usuário também tenta tocar
     * para abrir os detalhes. Medido no aparelho pelo nó clicável, não pelo desenho:
     * o disco preto continua com 40 dp, quem cresceu foi a área que aceita o toque.
     */
    @Test
    fun thePlayButtonOverAnEpisodeThumbnailMeetsTheMinimumTouchTarget() {
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
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "ep-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                            indexNumber = 1, parentIndexNumber = 1,
                        )
                    ),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                )
            }
        }

        val target = EPISODE_PLAY_TARGET_DP.dp
        composeRule
            .onNodeWithContentDescription("Reproduzir")
            .assertIsDisplayed()
            .assertHasClickAction()
            .assertWidthIsAtLeast(target)
            .assertHeightIsAtLeast(target)
    }
}
