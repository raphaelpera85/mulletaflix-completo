package org.mulletaflix.feature.itemdetail

import android.content.res.Configuration
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertHeightIsAtLeast
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsNotFocused
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
import androidx.compose.runtime.mutableStateOf
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
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

    private fun isTelevisionProfile(): Boolean {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        return (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
    }

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
    fun hidesSeasonDownloadActionOnTelevision() {
        if (!isTelevisionProfile()) return
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "sea-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "ep-1", "Piloto", org.mulletaflix.domain.model.MediaItemType.Episode,
                        ),
                    ),
                    selectedSeasonIndex = 0,
                    onSeasonSelect = {},
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                )
            }
        }

        composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
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
                    onDownloadSeason = { downloadRequests++; 1L },
                )
            }
        }

        if (isTelevisionProfile()) {
            composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
            return
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

        if (isTelevisionProfile()) {
            composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
            return
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

        if (isTelevisionProfile()) {
            composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
            return
        }

        composeRule.onNodeWithText("Preparando 1/2").assertIsNotEnabled()
        composeRule.onNodeWithText("Cancelar")
            .assertIsDisplayed()
            .assertIsEnabled()
            .assertHasClickAction()
    }

    @Test
    fun activeBatchRemainsVisibleAndCancellableAfterSwitchingSeasons() {
        var cancelRequests = 0
        var downloadRequests = 0
        val selectedSeasonIndex = mutableStateOf(0)
        val progress = mutableStateOf<SeasonDownloadProgress?>(null)
        composeRule.setContent {
            MulletaFlixTheme {
                SeriesSection(
                    seasons = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "season-1", "Temporada 1", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                        org.mulletaflix.domain.model.MediaItem(
                            "season-2", "Temporada 2", org.mulletaflix.domain.model.MediaItemType.Season,
                        ),
                    ),
                    episodes = listOf(
                        org.mulletaflix.domain.model.MediaItem(
                            "episode-3", "Episódio", org.mulletaflix.domain.model.MediaItemType.Episode,
                            indexNumber = 1, parentIndexNumber = 2,
                        ),
                    ),
                    selectedSeasonIndex = selectedSeasonIndex.value,
                    onSeasonSelect = { selectedSeasonIndex.value = it },
                    onEpisodePlay = {},
                    onEpisodeClick = {},
                    seasonDownloadProgress = progress.value,
                    onDownloadSeason = {
                        downloadRequests++
                        progress.value = SeasonDownloadProgress(
                            seasonId = "season-1",
                            seasonName = "Temporada 1",
                            totalEpisodes = 3,
                            processedEpisodes = 1,
                            queuedEpisodes = 1,
                            isRunning = true,
                            operationId = 11L,
                        )
                        11L
                    },
                    onCancelSeasonDownload = { cancelRequests++ },
                )
            }
        }

        if (isTelevisionProfile()) {
            composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
            return
        }

        composeRule.onNodeWithText("Baixar temporada").performClick()
        composeRule.onNodeWithText("Temporada 2").performClick()
        composeRule.waitForIdle()

        composeRule.onNodeWithText("Temporada 2").assertIsSelected()
        composeRule.onNodeWithText("Preparando 1/3").assertIsDisplayed().assertIsNotEnabled()
        composeRule.onNodeWithText("Preparação em andamento para Temporada 1").assertIsDisplayed()
        composeRule.onNodeWithText("Cancelar").assertIsDisplayed().assertIsEnabled().performClick()
        org.junit.Assert.assertEquals(1, downloadRequests)
        org.junit.Assert.assertEquals(1, cancelRequests)
    }

    @Test
    fun allSeasonDownloadActionsAreHiddenOnTvEvenWhenBatchIsRunning() {
        if (!isTelevisionProfile()) return

        var cancelRequests = 0
        val runningProgress = SeasonDownloadProgress(
            seasonId = "season-1",
            seasonName = "Temporada 1",
            totalEpisodes = 2,
            processedEpisodes = 1,
            queuedEpisodes = 1,
            isRunning = true,
        )
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
                    seasonDownloadProgress = runningProgress,
                    onCancelSeasonDownload = { cancelRequests++ },
                )
            }
        }

        val selectedSeason = composeRule.onNodeWithText("Temporada 1")
        selectedSeason.requestFocus()
        selectedSeason.assertIsFocused()
        composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
        composeRule.onNodeWithText("Preparando 1/2").assertDoesNotExist()
        composeRule.onNodeWithText("Cancelar").assertDoesNotExist()
        org.junit.Assert.assertEquals(0, cancelRequests)
        composeRule.onNodeWithText("Temporada 1").assertIsFocused()
    }

    @Test
    fun externalInProgressDownloadDoesNotStealTvFocusOnEntry() {
        if (!isTelevisionProfile()) return
        val entryFocusRequester = FocusRequester()
        val showExternalProgress = mutableStateOf(false)
        val progress = SeasonDownloadProgress(
            seasonId = "season-1",
            seasonName = "Temporada 1",
            totalEpisodes = 2,
            processedEpisodes = 1,
            queuedEpisodes = 1,
            isRunning = true,
        )

        composeRule.setContent {
            MulletaFlixTheme {
                Column {
                    Button(
                        onClick = {},
                        modifier = Modifier.focusRequester(entryFocusRequester),
                    ) { Text("Outra ação") }
                    if (showExternalProgress.value) {
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
                            seasonDownloadProgress = progress,
                        )
                    }
                }
            }
        }

        val otherAction = composeRule.onNodeWithText("Outra ação")
        otherAction.requestFocus()
        composeRule.waitForIdle()
        otherAction.assertIsFocused()
        composeRule.runOnIdle { showExternalProgress.value = true }
        composeRule.waitForIdle()
        otherAction.assertIsFocused()
        composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
        composeRule.onNodeWithText("Preparando 1/2").assertDoesNotExist()
        composeRule.onNodeWithText("Cancelar").assertDoesNotExist()
    }

    @Test
    fun externalBatchDoesNotRevealTvDownloadActionOrStealFocus() {
        if (!isTelevisionProfile()) return

        val entryFocusRequester = FocusRequester()
        val progress = mutableStateOf<SeasonDownloadProgress?>(null)
        composeRule.setContent {
            MulletaFlixTheme {
                Column {
                    Button(
                        onClick = {},
                        modifier = Modifier.focusRequester(entryFocusRequester),
                    ) { Text("Outra ação") }
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
                        seasonDownloadProgress = progress.value,
                        onDownloadSeason = {
                            progress.value = SeasonDownloadProgress(
                                seasonId = "season-1",
                                seasonName = "Temporada 1",
                                totalEpisodes = 1,
                                processedEpisodes = 1,
                                isRunning = false,
                                operationId = 31L,
                            )
                            31L
                        },
                    )
                }
            }
        }

        val otherAction = composeRule.onNodeWithText("Outra ação")
        otherAction.requestFocus()
        composeRule.waitForIdle()
        otherAction.assertIsFocused()
        composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()

        composeRule.runOnIdle {
            progress.value = SeasonDownloadProgress(
                seasonId = "season-1",
                seasonName = "Temporada 1",
                totalEpisodes = 1,
                processedEpisodes = 0,
                isRunning = true,
                operationId = 32L,
            )
        }
        composeRule.waitForIdle()
        otherAction.assertIsFocused()
        composeRule.onNodeWithText("Baixar temporada").assertDoesNotExist()
        composeRule.onNodeWithText("Preparando 0/1").assertDoesNotExist()
        composeRule.onNodeWithText("Cancelar").assertDoesNotExist()
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
